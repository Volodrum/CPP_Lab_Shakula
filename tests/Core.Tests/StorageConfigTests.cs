using Core.Configuration;
using Core.Domain;
using Core.Services;
using Core.Storage;

namespace Core.Tests;

// Тиждень 9: шляхи з конфігурації, JSON колекції (атомарний запис, кирилиця, пошкоджений файл), строк видачі з Library:LoanDays.
// Кожен тест працює у власному тимчасовому каталозі й прибирає його в Dispose.
public sealed class StorageConfigTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"crossapp-tests-{Guid.NewGuid():N}");

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    [Fact] // відносний DataPath рахується від переданого базового каталогу, а не від cwd; каталог створюється
    public void ResolveFullPath_Relative_CombinesWithBaseDir_AndCreatesDirectory()
    {
        var storage = new StorageOptions { DataPath = Path.Combine("nested", "data"), FileName = "items.json" };

        string path = storage.ResolveFullPath(_dir);

        Assert.Equal(Path.Combine(_dir, "nested", "data", "items.json"), path);
        Assert.True(Directory.Exists(Path.Combine(_dir, "nested", "data")));
    }

    [Fact] // абсолютний DataPath (як із CROSSAPP_DATA_PATH) базовий каталог ігнорує
    public void ResolveFullPath_Rooted_IgnoresBaseDir()
    {
        var storage = new StorageOptions { DataPath = _dir };

        string path = storage.ResolveFullPath(Path.Combine(_dir, "ignored"));

        Assert.Equal(Path.Combine(_dir, "library.json"), path);
        Assert.False(Directory.Exists(Path.Combine(_dir, "ignored")));
    }

    [Theory] // FileName — лише ім'я: каталог задається DataPath, інакше «..» вивело б файл за межі сховища
    [InlineData("")]
    [InlineData("../evil.json")]
    public void ResolveFullPath_FileNameWithDirectory_Throws(string fileName)
    {
        var storage = new StorageOptions { FileName = fileName };

        Assert.Throws<ArgumentException>(() => storage.ResolveFullPath(_dir));
    }

    [Fact] // експорт → імпорт повертає ту саму колекцію; кирилиця лишається читабельною; .tmp не лишається
    public void LibraryJson_RoundTrip_KeepsCollection_CyrillicReadable_NoTmpLeft()
    {
        string path = Path.Combine(_dir, "data", "library.json");
        var items = SampleData.Items();

        int written = LibraryJson.Write(path, items);
        var restored = LibraryJson.Read(path);

        Assert.Equal(items.Count, written);
        Assert.Equal(items.Select(i => i.Id).Order(), restored.Select(i => i.Id).Order());
        Assert.Equal(items.OfType<Loan>().Count(l => l.IsActive), restored.OfType<Loan>().Count(l => l.IsActive));
        string text = File.ReadAllText(path);
        Assert.Contains("Прагматичний програміст", text);
        Assert.DoesNotContain("\\u04", text);
        Assert.Contains("\"isbn\"", text); // camelCase
        Assert.False(File.Exists(path + ".tmp"));
    }

    [Fact] // повторний запис замінює файл повністю (File.Move з overwrite), а не дописує
    public void LibraryJson_Write_OverwritesExistingFile()
    {
        string path = Path.Combine(_dir, "library.json");
        LibraryJson.Write(path, SampleData.Items());

        LibraryJson.Write(path, [Book.Create("B-1", "978-0-13-235088-4", "Чистий код", 2008)]);

        Assert.Single(LibraryJson.Read(path));
    }

    [Fact] // зламаний синтаксис — StorageException з іменем файлу і JsonException усередині
    public void LibraryJson_Read_CorruptedJson_ThrowsStorageException()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "broken.json");
        File.WriteAllText(path, "[ { \"kind\": \"book\", \"id\": \"B-1\" ,, ]");

        var ex = Assert.Throws<StorageException>(() => LibraryJson.Read(path));

        Assert.Contains("broken.json", ex.Message);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(ex.InnerException);
    }

    [Fact] // список видань тижня 3 (без "kind") — не файл колекції: зрозуміла StorageException, а не NotSupportedException
    public void LibraryJson_Read_ListWithoutKind_ThrowsStorageException()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "catalog.json");
        File.WriteAllText(path, """[ { "id": "B-1", "isbn": "978-0-13-235088-4", "title": "Ц", "year": 1999 } ]""");

        var ex = Assert.Throws<StorageException>(() => LibraryJson.Read(path));

        Assert.Contains("kind", ex.Message);
    }

    [Fact] // синтаксис правильний, але запис порушує правила Book — теж StorageException, а не ArgumentException
    public void LibraryJson_Read_InvalidRecord_ThrowsStorageException()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "bad.json");
        File.WriteAllText(path, """[ { "kind": "book", "id": "B-1", "isbn": "123", "title": "Ц", "year": 1999 } ]""");

        var ex = Assert.Throws<StorageException>(() => LibraryJson.Read(path));

        Assert.IsAssignableFrom<ArgumentException>(ex.InnerException);
    }

    [Fact] // файли тижнів 5–8 (PascalCase, без loanDays) відкриваються: регістр не важить, строк — за замовчуванням
    public void LibraryJson_Read_LegacyPascalCaseFile_UsesDefaultLoanDays()
    {
        Directory.CreateDirectory(_dir);
        string path = Path.Combine(_dir, "legacy.json");
        File.WriteAllText(path, """
            [
              { "kind": "copy", "Id": "C-1", "Isbn": "978-0-13-235088-4", "IsIssued": true },
              { "kind": "loan", "Id": "L-1", "CopyId": "C-1", "ReaderId": "R-1", "IssuedOn": "2026-09-01", "ReturnedOn": null, "Status": "Open" }
            ]
            """);

        Loan loan = Assert.Single(LibraryJson.Read(path).OfType<Loan>());

        Assert.Equal(Loan.DefaultLoanDays, loan.LoanDays);
        Assert.Equal(new DateOnly(2026, 9, 15), loan.DueOn);
    }

    [Fact] // FileBookStore пише через LibraryJson: новий екземпляр на тому ж шляху бачить збережене
    public void FileBookStore_PersistsBetweenInstances()
    {
        string path = new StorageOptions { DataPath = "data" }.ResolveFullPath(_dir);
        new LendingService(new FileBookStore(path)).AddBook("978-0-13-235088-4", "Чистий код", 2008);

        var reopened = new FileBookStore(path);

        Assert.Equal("Чистий код", Assert.Single(reopened.List<Book>()).Title);
    }

    [Fact] // Library:LoanDays: строк нових видач береться з налаштування і переживає збереження у файл
    public void LendingService_UsesConfiguredLoanDays_AndFileKeepsIt()
    {
        string path = Path.Combine(_dir, "library.json");
        var service = new LendingService(new FileBookStore(path), loanDays: 21);
        service.AddBook("978-0-13-235088-4", "Чистий код", 2008);
        BookCopy copy = service.RegisterCopy("978-0-13-235088-4");

        Loan loan = service.IssueCopy(copy.Id, "R-1", new DateOnly(2026, 9, 1));
        Loan reloaded = new FileBookStore(path).GetById<Loan>(loan.Id)!;

        Assert.Equal(new DateOnly(2026, 9, 22), loan.DueOn);
        Assert.Equal(21, reloaded.LoanDays);
    }

    [Theory] // строк поза 1..365 — помилка налаштування, яку видно одразу при створенні сервісу
    [InlineData(0)]
    [InlineData(400)]
    public void LendingService_InvalidLoanDays_Throws(int days)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LendingService(new InMemoryBookStore(), loanDays: days));
    }
}

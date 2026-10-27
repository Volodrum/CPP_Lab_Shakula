using System.Text.Json;
using Core.Domain;
using Core.Events;
using Core.Services;
using Core.Storage;

namespace Core.Tests;

// Асинхронний імпорт: скасування, ідемпотентність, події, розмежування доменних і технічних помилок.
// Тести з файлом пишуть унікальний тимчасовий файл у Path.GetTempPath() і видаляють його у finally; data/ не читається.
public class ImportTests
{
    [Fact] // скасування: заздалегідь скасований токен — файл навіть не відкривається
    public async Task ImportAsync_WhenAlreadyCancelled_Throws()
    {
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();
        var service = new LendingService(new InMemoryBookStore());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(new JsonCatalogImporter(), "any.json", cts.Token));
    }

    [Fact] // токен перевіряється між записами: скасування посеред циклу зупиняє імпорт
    public async Task ImportAsync_WhenCancelledDuringLoop_StopsAfterCurrentRecord()
    {
        var store = new InMemoryBookStore();
        var service = new LendingService(store);
        using var cts = new CancellationTokenSource();
        var progress = new CallbackProgress(_ => cts.Cancel()); // скасовуємо після першого ж запису

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.ImportAsync(new FakeImporter([.. TestData.Books()]), "fake", progress, cts.Token));

        Assert.Single(store.List<Book>());
    }

    [Fact] // подія — на кожен доданий запис; повторний імпорт нічого не дублює
    public async Task ImportAsync_RaisesEventPerAddedBook_AndIsIdempotent()
    {
        var store = new InMemoryBookStore();
        var service = new LendingService(store);
        var importer = new FakeImporter([.. TestData.Books()]);
        var events = new List<LibraryChangedEventArgs>();
        service.Changed += (_, e) => events.Add(e);

        int first = await service.ImportAsync(importer, "fake");
        int second = await service.ImportAsync(importer, "fake");

        Assert.Equal((4, 0), (first, second));
        Assert.Equal(4, events.Count(e => e.Kind == LibraryChangeKind.BookAdded));
        Assert.Equal(4, store.List<Book>().Count);
    }

    [Fact] // інший id, але ISBN уже в каталозі — пропускаємо, а не порушуємо унікальність
    public async Task ImportAsync_SkipsBookWithIsbnAlreadyInCatalog()
    {
        var service = new LendingService(new InMemoryBookStore());
        service.AddBook(TestData.BigIsbn, "Уже в каталозі", 2001);
        var importer = new FakeImporter(Book.Create("B-NEW", "9780000000010", "Те саме видання", 2001));

        Assert.Equal(0, await service.ImportAsync(importer, "fake"));
    }

    [Fact] // зіпсований запис → DomainException з номером запису, причина — в InnerException
    public async Task LoadAsync_FileWithInvalidRecord_ThrowsDomainException_WithRecordNumber() =>
        await WithTempFileAsync(CatalogJson(("B-1", TestData.BigIsbn, 2001), ("B-2", TestData.MidIsbn, 2099)), async path =>
        {
            var ex = await Assert.ThrowsAsync<DomainException>(() => new JsonCatalogImporter().LoadAsync(path));

            Assert.StartsWith("Запис 2:", ex.Message);
            Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
        });

    [Fact] // список з LoadAsync атомарний: поганий запис — у каталог не потрапляє жоден
    public async Task ImportAsync_FromFileWithInvalidRecord_AddsNothing() =>
        await WithTempFileAsync(CatalogJson(("B-1", TestData.BigIsbn, 2001), ("B-2", "123", 2001)), async path =>
        {
            var store = new InMemoryBookStore();
            var service = new LendingService(store);

            await Assert.ThrowsAsync<DomainException>(() => service.ImportAsync(new JsonCatalogImporter(), path));
            Assert.Empty(store.List<Book>());
        });

    [Fact] // потоковий імпорт (IAsyncEnumerable) не атомарний: записи до поганого вже додано
    public async Task StreamImport_StopsAtInvalidRecord_KeepingEarlierOnes() =>
        await WithTempFileAsync(
            CatalogJson(("B-1", TestData.BigIsbn, 2001), ("B-2", TestData.MidIsbn, 1999), ("B-3", "123", 2001)), async path =>
        {
            var store = new InMemoryBookStore();
            var service = new LendingService(store);

            var ex = await Assert.ThrowsAsync<DomainException>(
                () => service.ImportAsync(new JsonCatalogImporter().StreamAsync(path)));

            Assert.StartsWith("Запис 3:", ex.Message);
            Assert.Equal(2, store.List<Book>().Count);
        });

    [Fact] // технічна помилка не маскується під доменну: StorageException, а JsonException — в InnerException
    public async Task LoadAsync_MalformedJson_ThrowsStorageException_KeepingJsonInner() =>
        await WithTempFileAsync("[ { \"id\": \"B-1\", ", async path =>
        {
            var ex = await Assert.ThrowsAsync<StorageException>(() => new JsonCatalogImporter().LoadAsync(path));

            Assert.IsAssignableFrom<JsonException>(ex.InnerException);
        });

    [Fact] // дод. завдання 2: зв'язані токени — спрацював саме тайм-аут, а не користувач
    public async Task TimeoutPolicy_WhenTimeoutElapses_ThrowsTimeoutException()
    {
        var service = new LendingService(new InMemoryBookStore());
        using var user = new CancellationTokenSource();

        var ex = await Assert.ThrowsAsync<TimeoutException>(() => TimeoutPolicy.RunAsync(
            ct => service.ImportAsync(new NeverEndingImporter(), "slow", ct), TimeSpan.FromMilliseconds(50), user.Token));

        Assert.False(user.IsCancellationRequested);
        Assert.IsAssignableFrom<OperationCanceledException>(ex.InnerException);
    }

    [Fact] // і навпаки: скасування користувачем проходить як OperationCanceledException, не як тайм-аут
    public async Task TimeoutPolicy_WhenUserCancels_ThrowsOperationCanceled()
    {
        var service = new LendingService(new InMemoryBookStore());
        using var user = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => TimeoutPolicy.RunAsync(
            ct => service.ImportAsync(new NeverEndingImporter(), "slow", ct), TimeSpan.FromMinutes(5), user.Token));
    }

    private static string CatalogJson(params (string Id, string Isbn, int Year)[] rows) =>
        JsonSerializer.Serialize(rows.Select(r => new { id = r.Id, isbn = r.Isbn, title = $"Видання {r.Id}", year = r.Year }));

    // Унікальна назва — тести можуть іти паралельно; finally — файл зникає навіть після провалу Assert.
    private static async Task WithTempFileAsync(string content, Func<string, Task> test)
    {
        string path = Path.Combine(Path.GetTempPath(), $"core-tests-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, content);
        try
        {
            await test(path);
        }
        finally
        {
            File.Delete(path);
        }
    }
}

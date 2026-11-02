using Core;
using Core.Domain;
using Core.Reports;
using Core.Services;
using Core.Storage;

// Лабораторна 9: команди config / export / import. Шлях до файлу — з конфігурації (AppSettings.DataFile),
// формат і атомарний запис — у Core.Storage.LibraryJson (той самий, що й у FileBookStore --file).
internal static class CollectionCommands
{
    // config — що звідки прочитано; зручно перевіряти перевизначення змінними середовища.
    public static int ShowConfig(AppSettings s)
    {
        PrintHeader(s);
        Console.WriteLine($"Середовище      : {s.EnvironmentName} (EnvironmentName з файлу: {s.Options.EnvironmentName})");
        Console.WriteLine($"Рівень журналу  : {s.Log.Minimum}");
        Console.WriteLine($"Storage:Root    : {s.Storage.Root} → {s.Storage.BaseDirectory()}");
        Console.WriteLine($"Storage:DataPath: {s.Storage.DataPath}" +
                          (s.DataPathFromLegacyVariable ? $"  (з {AppSettings.LegacyDataPathVariable})" : ""));
        Console.WriteLine($"Storage:FileName: {s.Storage.FileName}");
        Console.WriteLine($"Library:LoanDays: {s.Options.Library.LoanDays}");
        return 0;
    }

    // export [шлях] — уся колекція (видання, примірники, видачі) у JSON. Без шляху — у файл даних із конфігурації.
    public static int Export(AppSettings s, string? target)
    {
        PrintHeader(s);
        string path = target is null ? s.DataFile : Path.GetFullPath(target); // шлях з консолі — відносно cwd, як очікує користувач
        var items = SampleData.Items();
        int written = LibraryJson.Write(path, items);
        if (target is not null) Console.WriteLine($"Експорт у     : {path}");
        Console.WriteLine($"Експортовано {Records(written)} " +
                          $"(видань {items.OfType<Book>().Count()}, примірників {items.OfType<BookCopy>().Count()}, " +
                          $"видач {items.OfType<Loan>().Count()}).");
        s.Log.Debug($"Тимчасовий файл {Path.GetFileName(path)}.tmp після File.Move: " +
                    $"{(File.Exists(path + ".tmp") ? "лишився" : "немає")}");
        return 0;
    }

    // import [шлях] — читає колекцію в сховище в пам'яті і перевіряє, що вона придатна для звітів.
    public static int Import(AppSettings s, string? source)
    {
        PrintHeader(s);
        string path = source is null ? s.DataFile : Path.GetFullPath(source);
        if (source is not null) Console.WriteLine($"Імпорт з      : {path}");
        try
        {
            var items = LibraryJson.Read(path);
            var service = new LendingService(new InMemoryBookStore(items), loanDays: s.Options.Library.LoanDays);
            Console.WriteLine($"Імпортовано {Records(items.Count)}.");

            Book[] books = [.. service.All<Book>()];
            var totals = ReportService.Totals(books, service.All<BookCopy>());
            Console.WriteLine($" видань {totals.Books}, примірників {totals.Copies}, видач {service.All<Loan>().Count}; " +
                              $"прострочено на сьогодні: {ReportService.Overdue(service.All<Loan>(), DateOnly.FromDateTime(DateTime.Today)).Count}");
            Book? first = books.OrderBy(b => b.Id, StringComparer.Ordinal).FirstOrDefault();
            if (first is not null) Console.WriteLine($" перше видання: {first}");
            return 0;
        }
        catch (FileNotFoundException)
        {
            Console.WriteLine($"Файл не знайдено: {path}. Спершу виконайте export.");
        }
        catch (StorageException ex) // зіпсований JSON або запис, що порушує правила домену, — без stack trace
        {
            Console.WriteLine($"Файл пошкоджено: {ex.Message}");
            s.Log.Debug($"Причина: {ex.InnerException?.GetType().Name}");
        }
        return 1;
    }

    private static void PrintHeader(AppSettings s)
    {
        Console.WriteLine($"Файл даних : {s.DataFile}");
        Console.WriteLine($"ApiKey: {s.ApiKeyStatus}");
    }

    // 1 запис, 2–4 записи, 5+ записів (11–14 — теж «записів»).
    private static string Records(int n) => (n % 100 is >= 11 and <= 14 ? 0 : n % 10) switch
    {
        1 => $"{n} запис",
        2 or 3 or 4 => $"{n} записи",
        _ => $"{n} записів"
    };
}

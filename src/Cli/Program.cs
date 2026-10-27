using System.Text;
using Core;
using Core.Abstractions;
using Core.Domain;
using Core.Reports;
using Core.Services;
using Core.Storage;

Console.OutputEncoding = Encoding.UTF8;

// ---------- Режими попередніх лабораторних ----------
switch (args)
{
    case ["--info" or "--env", ..]:
        return PrintEnvironment();
    case ["--domain", ..]:
        // Лабораторна 4; з тижня 5 сервіс отримує сховище ззовні.
        return DomainDemo.Run(args.Length > 1 ? args[1] : Path.Combine("data", "books_domain.csv"),
            new LendingService(new InMemoryBookStore()));
    case ["--import", ..]:
        return ImportDemo.Run(args.Length > 1 ? args[1] : Path.Combine("data", "sample.csv"));
    case [var file, ..] when !file.StartsWith("--") && !file.StartsWith("report"):
        return ImportDemo.Run(file);
}

// ---------- Composition root (лаб. 5): єдине місце, де обирається реалізація сховища ----------
string dataPath = Path.Combine(AppContext.BaseDirectory, "data", "library.json");
IBookStore store = StoreFactory.Create(args, dataPath); // --file → FileBookStore, --empty → порожнє, інакше SampleData; --cache → декоратор

Console.WriteLine($"Сховище: {(store is CachingBookStore ? store.ToString() : store.GetType().Name)}");
if (args.Contains("--file"))
    Console.WriteLine($"Файл: {dataPath} ({(File.Exists(dataPath) ? "є з попереднього запуску" : "ще не створено")})");

if (args.Contains("--service"))
    return ServiceDemo.Run(new LendingService(store), store);

if (args.Contains("--sinks")) // лаб. 6, дод. завдання 1: одержувачі сповіщень через конструктор замість події
{
    string sinkLogPath = Path.Combine(AppContext.BaseDirectory, "logs", "sinks.log");
    return SinkDemo.Run(new LendingService(store, new CompositeSink(new ConsoleSink(), new FileSink(sinkLogPath))), sinkLogPath);
}

var service = new LendingService(store);

if (args.Contains("--events")) // лаб. 6: доменні події (раніше запускалась без ключів)
    return EventsDemo.Run(service);

if (args.Contains("--import-async")) // лаб. 8: асинхронний імпорт каталогу; top-level await — Main сам став async Task<int>
    return await ImportCatalogAsync(service, AsyncImportOptions.Parse(args));

// ---------- Лабораторна 7: звіти LINQ ----------
// Program.cs лише обирає звіт і друкує DTO; жодного Where/GroupBy тут немає — усі запити в Core/Reports.
const int TopN = 5;
const int LowShelfThreshold = 2;
var today = DateOnly.FromDateTime(DateTime.Today);

Book[] books = [.. service.All<Book>()];
BookCopy[] copies = [.. service.All<BookCopy>()];
Loan[] loans = [.. service.All<Loan>()];
bool shuffle = args.Contains("--shuffle");
if (shuffle) // перевірка стабільності: інший порядок джерела — той самий звіт
{
    Random.Shared.Shuffle(books);
    Random.Shared.Shuffle(copies);
    Random.Shared.Shuffle(loans);
}
Console.WriteLine($"Дані: видань {books.Length}, примірників {copies.Length}, видач {loans.Length}; " +
                  $"дата звіту {today:dd.MM.yyyy}{(shuffle ? "; порядок джерела перемішано" : "")}");
Console.WriteLine();

var command = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "report-top";
string[] all = ["report-top", "report-overdue", "report-low", "report-groups", "report-sum", "report-books", "report-join", "report-query"];

foreach (string report in command == "report-all" ? all : [command])
    RunReport(report);
return 0;

void RunReport(string report)
{
    switch (report)
    {
        case "report-top":
            Header($"Топ-{TopN} читачів за кількістю видач");
            var top = ReportService.TopReaders(loans, TopN);
            if (NoRows(top)) break;
            Console.WriteLine($" {"Читач",-8} {"видач",6} {"незакр.",8}");
            foreach (var r in top)
                Console.WriteLine($" {r.ReaderId,-8} {r.Loans,6} {r.ActiveLoans,8}");
            break;
        case "report-overdue":
            Header($"Прострочені видачі (строк {Loan.LoanPeriodDays} днів, на {today:dd.MM.yyyy})");
            PrintLoans(ReportService.Overdue(loans, today));
            break;
        case "report-low":
            Header($"Видання, яких на полиці менше {LowShelfThreshold} примірників");
            PrintStock(ReportService.LowStock(books, copies, LowShelfThreshold));
            break;
        case "report-groups":
            Header("Видання за десятиліттями");
            var groups = ReportService.ByDecade(books, copies);
            if (NoRows(groups)) break;
            foreach (var g in groups)
                Console.WriteLine($" {g.Key,-8} видань: {g.Count,3}  примірників: {g.TotalCopies,4}");
            break;
        case "report-sum":
            Header("Підсумки фонду");
            var t = ReportService.Totals(books, copies);
            Console.WriteLine($" видань {t.Books}, примірників {t.Copies}, середнє на видання {t.AverageCopies:F2}, макс {t.MaxCopies}");
            break;
        case "report-books":
            Header($"Топ-{TopN} видань за кількістю примірників (TopBy)");
            PrintStock(ReportService.TopBooks(books, copies, TopN));
            break;
        case "report-join":
            Header("Прострочені видачі з назвами видань (Join видачі × видання)");
            var joined = ReportService.OverdueWithTitles(loans, books, today);
            if (NoRows(joined)) break;
            foreach (var o in joined)
                Console.WriteLine($" {o.LoanId,-6} {o.ReaderId,-6} {Shorten(o.Title, 34),-34} до {o.DueOn:dd.MM.yyyy} {o.DaysOverdue,4} дн.");
            break;
        case "report-query":
            Header("Видання за десятиліттями — синтаксис запитів (from … group … into …)");
            var byQuery = ReportService.ByDecadeQuery(books, copies);
            foreach (var g in byQuery)
                Console.WriteLine($" {g.Key,-8} видань: {g.Count,3}  примірників: {g.TotalCopies,4}");
            bool same = byQuery.SequenceEqual(ReportService.ByDecade(books, copies)); // record: порівняння за значенням
            Console.WriteLine($" Збігається з методним ByDecade поелементно: {(same ? "так" : "НІ")}");
            break;
        case "report-deferred":
            Header("Відкладене виконання");
            DeferredDemo.Run(service, today);
            break;
        case "report-bench":
            Header("Stopwatch: LowStock на згенерованих даних");
            string? size = args.Skip(1).FirstOrDefault(a => !a.StartsWith("--"));
            BenchDemo.Run(int.TryParse(size, out int count) ? count : 100_000);
            break;
        default:
            Console.WriteLine($" Невідома команда «{report}». Доступні:");
            Console.WriteLine($"  {string.Join(" | ", all)} | report-all | report-deferred | report-bench [кількість]");
            Console.WriteLine("  ключі: --file | --empty | --shuffle | --cache; режими попередніх лаб: --import-async (лаб. 8) --events --service --sinks --import --domain --info");
            break;
    }
    Console.WriteLine();
}

// ---------- Лабораторна 8: асинхронний імпорт, скасування, розмежування помилок ----------
// Токен створює Cli (тут), перевіряють LendingService.ImportAsync і JsonCatalogImporter.LoadAsync:
// cts.Token → ImportAsync(…, ct) → LoadAsync(path, ct) → DeserializeAsync(…, ct) + ThrowIfCancellationRequested у циклах.
static async Task<int> ImportCatalogAsync(LendingService service, AsyncImportOptions options)
{
    using var cts = new CancellationTokenSource();
    ConsoleCancelEventHandler onCtrlC = (_, e) =>
    {
        e.Cancel = true; // не вбивати процес одразу, а попросити імпорт зупинитися
        cts.Cancel();
    };
    Console.CancelKeyPress += onCtrlC;

    var importer = new JsonCatalogImporter();
    var progress = new ConsoleProgress();
    int events = 0;
    if (options.Big is null) // на мільйоні записів підписник рахував би полицю (CountOnShelf) мільйон разів
        service.Changed += (_, _) => events++;

    string path = options.Big is int count ? BigCatalog.TempPath(count) : options.Path;
    try
    {
        if (options.Big is int n)
        {
            Console.WriteLine($"Генерую {n:N0} видань у {path} …");
            await BigCatalog.WriteAsync(path, n, cts.Token);
        }
        Console.WriteLine($"Імпорт {path}{(options.Stream ? " (потоково, IAsyncEnumerable)" : "")}" +
                          $"{(options.Timeout is { } t ? $", тайм-аут {t.TotalSeconds:0.##} с" : "")}; Ctrl+C — скасувати");

        int added = options.Timeout is { } timeout
            ? await TimeoutPolicy.RunAsync(Import, timeout, cts.Token) // дод. 2: зв'язані токени Ctrl+C + тайм-аут
            : await Import(cts.Token);

        Say($"Імпортовано записів: {added}");
        if (options.Big is null)
            Console.WriteLine($"Подій Changed: {events}; видань у каталозі: {service.All<Book>().Count}");
        return 0;
    }
    // Порядок важливий: скасування — очікуваний результат, а не збій, тож перехоплюється першим і окремо.
    catch (OperationCanceledException) { Say("Імпорт скасовано."); }
    catch (TimeoutException ex) { Say($"Імпорт скасовано: {ex.Message}"); }
    catch (DomainException ex) { Say($"Дані відхилено: {ex.Message}"); }
    catch (StorageException ex) { Say($"Файл пошкоджено: {ex.Message}"); }
    catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { Say($"Файл не знайдено: {path}"); }
    finally
    {
        Console.CancelKeyPress -= onCtrlC; // після виходу cts уже звільнено — обробник не повинен до нього звертатися
        if (options.Big is not null) File.Delete(path);
    }
    return 1;

    Task<int> Import(CancellationToken ct) => options.Stream
        ? service.ImportAsync(importer.StreamAsync(path), ct) // токен дійде до StreamAsync через WithCancellation
        : service.ImportAsync(importer, path, progress, ct);

    void Say(string message)
    {
        progress.EndLine();
        Console.WriteLine(message);
    }
}

static void Header(string title) => Console.WriteLine($"=== {title} ===");

// Порожнє джерело — не помилка: звіт повертає порожній список, а вивід про це каже.
static bool NoRows<T>(IReadOnlyList<T> rows)
{
    if (rows.Count == 0) Console.WriteLine(" (немає рядків)");
    return rows.Count == 0;
}

static void PrintStock(IReadOnlyList<StockLine> lines)
{
    if (NoRows(lines)) return;
    Console.WriteLine($" {"Id",-6} {"ISBN",-18} {"Назва",-34} {"рік",4} {"прим.",5} {"на пол.",7}");
    foreach (var l in lines)
        Console.WriteLine($" {l.BookId,-6} {l.Isbn,-18} {Shorten(l.Title, 34),-34} {l.Year,4} {l.Copies,5} {l.OnShelf,7}");
}

static void PrintLoans(IReadOnlyList<LoanLine> lines)
{
    if (NoRows(lines)) return;
    Console.WriteLine($" {"Видача",-6} {"Прим.",-6} {"Читач",-6} {"видано",10} {"строк до",10} {"простр.",8}");
    foreach (var l in lines)
        Console.WriteLine($" {l.LoanId,-6} {l.CopyId,-6} {l.ReaderId,-6} {l.IssuedOn,10:dd.MM.yyyy} {l.DueOn,10:dd.MM.yyyy} {l.DaysOverdue,5} дн.");
}

static string Shorten(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

static int PrintEnvironment()
{
    EnvironmentReport report = EnvironmentInfo.Collect();
    Console.WriteLine("CrossApp – Інформація про середовище");
    Console.WriteLine($"Студент: Шакула Володимир, група ФЕІ-32с");
    Console.WriteLine(new string('-', 56));
    Console.WriteLine($"ОС (OSDescription)  : {report.OsDescription}");
    Console.WriteLine($"Runtime             : {report.FrameworkDescription}");
    Console.WriteLine($"Архітектура процесу : {report.ProcessArchitecture}");
    Console.WriteLine($"RID (визначено)     : {report.DetectedRid}");
    Console.WriteLine($"RID (від .NET)      : {report.ReportedRid}");
    Console.WriteLine($"Каталог застосунку  : {report.BaseDirectory}");
    Console.WriteLine($"Цільова збірка Core : {report.BuildTargetNote}");
    return 0;
}

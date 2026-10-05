using System.Text;
using Core;
using Core.Abstractions;
using Core.Domain;
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
    case [var file, ..] when !file.StartsWith("--"):
        return ImportDemo.Run(file);
}

// ---------- Лабораторна 5: composition root — єдине місце, де обирається реалізація сховища ----------
string dataPath = Path.Combine(AppContext.BaseDirectory, "data", "library.json");
IBookStore store = StoreFactory.Create(args, dataPath); // --file → FileBookStore, інакше InMemoryBookStore; --cache → декоратор
var service = new LendingService(store);

Console.WriteLine($"Сховище: {(store is CachingBookStore ? store.ToString() : store.GetType().Name)}");
if (args.Contains("--file"))
    Console.WriteLine($"Файл: {dataPath} ({(File.Exists(dataPath) ? "є з попереднього запуску" : "ще не створено")})");
Console.WriteLine();

const string DemoIsbn = "978-0-13-235088-4";
var today = DateOnly.FromDateTime(DateTime.Today);

Console.WriteLine("=== 1. Додати: видання + 2 примірники ===");
Book book = service.FindByIsbn(DemoIsbn) ?? service.AddBook(DemoIsbn, "Чистий код", 2008, "Роберт Мартін");
BookCopy first = service.RegisterCopy(book.Isbn);
BookCopy second = service.RegisterCopy(book.Isbn);
Console.WriteLine($" {book}");
Console.WriteLine($" + {first}");
Console.WriteLine($" + {second}");
Console.WriteLine();

Console.WriteLine("=== 2. Змінити: видача примірника ===");
Loan loan = service.IssueCopy(first.Id, "R-010", today);
Console.WriteLine($" {loan}");
Console.WriteLine();

Console.WriteLine("=== 3. Список ===");
PrintCatalog(service);
Console.WriteLine();

Console.WriteLine("=== 4. Знайти за id ===");
Console.WriteLine($" {first.Id}: {service.Find<BookCopy>(first.Id)}");
Console.WriteLine($" {loan.Id.ToLowerInvariant()}: {service.Find<Loan>(loan.Id.ToLowerInvariant())}");
Console.WriteLine($" B-404: {service.Find<Book>("B-404")?.ToString() ?? "не знайдено"}");
Console.WriteLine();

Console.WriteLine("=== 5. Сценарій відмови ===");
TryDo("видача неіснуючого примірника", () => service.IssueCopy("C-404", "R-010", today));
TryDo("повернення неіснуючої видачі", () => service.ReturnCopy("L-404", today));
TryDo("повторна видача виданого примірника", () => service.IssueCopy(first.Id, "R-011", today));
TryDo("дубль ISBN у каталозі", () => service.AddBook(DemoIsbn, "Чистий код (копія)", 2008));
TryDo("дубль id у сховищі", () => store.Add(book));
TryDo("вилучення видання з примірниками", () => service.RemoveBook(book.Id));
Console.WriteLine($" Після відмов: {service.Find<BookCopy>(first.Id)}; видач у читача R-010: {service.CountActiveLoans("R-010")}");
Console.WriteLine();

Console.WriteLine("=== 6. Повернення ===");
Console.WriteLine($" {service.ReturnCopy(loan.Id, today)}");
Console.WriteLine($" {service.Find<BookCopy>(first.Id)}");
Console.WriteLine();

Console.WriteLine("=== Додаткове 2: пошук з Func<T, bool> ===");
Func<Book, bool> beforeYear2000 = b => b.Year < 2000;
IReadOnlyList<Book> classics = service.Search(beforeYear2000);
Console.WriteLine($" Видання до 2000 року: {classics.Count}");
foreach (Book b in classics)
    Console.WriteLine($"  {b}");
Console.WriteLine($" Примірників на полиці: {service.Search<BookCopy>(c => !c.IsIssued).Count}");
IReadOnlyList<Loan> lost = service.Search<Loan>(l => l.Status == LoanStatus.Lost);
Console.WriteLine($" Втрачені примірники: {(lost.Count == 0 ? "немає" : string.Join(", ", lost.Select(l => $"{l.Copy.Id} ({l.Id})")))}");

if (store is CachingBookStore cache)
{
    Console.WriteLine();
    Console.WriteLine($"=== Додаткове 1: кеш — влучань {cache.Hits}, промахів {cache.Misses} ===");
}

return 0;

static void PrintCatalog(LendingService service)
{
    IReadOnlyList<Book> books = service.All<Book>();
    IReadOnlyList<Loan> loans = service.All<Loan>();
    Console.WriteLine($" Видань: {books.Count}, примірників: {service.All<BookCopy>().Count}, " +
                      $"видач: {loans.Count} (незакритих {loans.Count(l => l.IsActive)})");

    foreach (Book b in books)
    {
        IReadOnlyList<BookCopy> copies = service.CopiesOf(b);
        Console.WriteLine($"  {b.Id,-10} {b.Isbn,-18} {Shorten(b.Title, 26),-26} {b.Year,4}" +
                          $"  прим.: {copies.Count}, на полиці: {copies.Count(c => !c.IsIssued)}");
    }

    foreach (Loan l in loans.Where(l => l.IsActive))
        Console.WriteLine($"  {l}");
}

static string Shorten(string text, int max) => text.Length <= max ? text : text[..(max - 1)] + "…";

static void TryDo(string title, Action action)
{
    try
    {
        action();
        Console.WriteLine($" {title}: відмови НЕ було!");
    }
    catch (Exception ex) when (ex is InvalidOperationException or ArgumentException)
    {
        Console.WriteLine($" ✗ {title}: {ex.Message}");
    }
}

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

using Core;
using Core.Domain;
using Core.Events;
using Core.Services;

// Лабораторна 6: сценарій доменних подій (запуск: dotnet run --project src/Cli -- --events [--file]).
// Перенесено з Program.cs без змін логіки; сервіс приходить із composition root.
internal static class EventsDemo
{
    public static int Run(LendingService service)
    {
        var logPath = Path.Combine(AppContext.BaseDirectory, "logs", "app.log");
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        Console.WriteLine($"Журнал: {logPath}");
        Console.WriteLine();

        int logged = 0; // скільки рядків цей запуск дописав у файл (локальна функція теж замикається на змінну)

        // Підписник 1: локальна функція — стале ім'я, тож пізніше можна зробити -= тим самим обробником.
        void OnLibraryChanged(object? sender, LibraryChangedEventArgs e)
        {
            string line = ChangeLog.Format(e);
            Console.WriteLine($" {line}");
            try
            {
                File.AppendAllText(logPath, line + Environment.NewLine);
                logged++;
            }
            catch (IOException ex) // збій журналу не повинен зламати бізнес-операцію, що вже відбулась
            {
                Console.WriteLine($" ! журнал недоступний: {ex.Message}");
            }
        }

        int changes = 0;
        var today = DateOnly.FromDateTime(DateTime.Today);

        Console.WriteLine("=== 1. Підписка: локальна функція (консоль + файл), лямбда-лічильник, тимчасова лямбда ===");
        service.Changed += OnLibraryChanged;
        service.Changed += (_, _) => changes++; // підписник 2: замикання над локальною змінною changes

        // Підписник 3: лямбда, збережена у змінній, — лише щоб показати порядок виклику і відписку лямбди.
        EventHandler<LibraryChangedEventArgs> orderProbe =
            (_, e) => Console.WriteLine($"   └ 3-й підписник: {e.Kind}; лічильник (2-й) уже = {changes}");
        service.Changed += orderProbe;
        Console.WriteLine();

        Console.WriteLine("=== 2. Успішні операції: кожна дає рівно одну подію ===");
        const string DemoIsbn = "978-0-13-595705-9";
        Book book = service.FindByIsbn(DemoIsbn) ?? service.AddBook(DemoIsbn, "Прагматичний програміст, 2-ге вид.", 2019, "Девід Томас");
        service.Changed -= orderProbe; // та сама змінна → той самий делегат → відписка спрацьовує
        BookCopy first = service.RegisterCopy(book.Isbn);
        BookCopy second = service.RegisterCopy(book.Isbn);
        Loan loan = service.IssueCopy(first.Id, "R-010", today);
        Console.WriteLine($" Подій отримано: {changes}");
        Console.WriteLine();

        Console.WriteLine("=== 3. Сценарій відмови: виняток є, рядка події немає ===");
        TryDo("повторна видача виданого примірника", () => service.IssueCopy(first.Id, "R-011", today));
        TryDo("видача неіснуючого примірника", () => service.IssueCopy("C-404", "R-010", today));
        TryDo("повернення раніше дати видачі", () => service.ReturnCopy(loan.Id, today.AddDays(-1)));
        TryDo("рік видання з майбутнього", () => service.AddBook("978-0-00-000000-2", "Книга майбутнього", 2099));
        TryDo("дубль ISBN у каталозі", () => service.AddBook(DemoIsbn, "Те саме видання", 2019));
        Console.WriteLine($" Подій отримано: {changes} (не змінилось)");
        Console.WriteLine();

        Console.WriteLine("=== 4. Відписка: операція виконується, повідомлення немає ===");
        service.Changed -= OnLibraryChanged;
        service.Changed -= (_, _) => changes++; // НЕ знімає підписника 2: нова лямбда — інший екземпляр делегата
        Loan returned = service.ReturnCopy(loan.Id, today); // тиха операція: підписка керована
        Console.WriteLine($" {returned}");
        Console.WriteLine($" {service.Find<BookCopy>(first.Id)}");
        Console.WriteLine($" Подій отримано: {changes} (+1 лише від лямбди-лічильника)");
        Console.WriteLine();

        Console.WriteLine("=== 5. Func замість події, коли потрібна відповідь ===");
        IReadOnlyList<Loan> overdue = service.Search<Loan>(l => l.IsActive && l.DueOn < today);
        Console.WriteLine($" Прострочені видачі (строк {Loan.LoanPeriodDays} днів): {overdue.Count}");
        foreach (Loan l in overdue)
            Console.WriteLine($"  {l.Id}: примірник {l.Copy.Id}, читач {l.ReaderId}, мав повернути до {l.DueOn:dd.MM.yyyy}");
        IReadOnlyList<Book> noneOnShelf = service.Search<Book>(b => service.CopiesOf(b).All(c => c.IsIssued));
        Console.WriteLine($" Видання без жодного примірника на полиці: " +
                          $"{(noneOnShelf.Count == 0 ? "немає" : string.Join(", ", noneOnShelf.Select(b => $"{b.Id} «{b.Title}»")))}");
        Console.WriteLine();

        Console.WriteLine($"=== 6. Що потрапило в logs/app.log за цей запуск ({logged} рядк.) ===");
        foreach (string line in File.ReadLines(logPath).TakeLast(logged))
            Console.WriteLine($" {line}");

        return 0;
    }

    private static void TryDo(string title, Action action)
    {
        try
        {
            action();
            Console.WriteLine($" {title}: відмови НЕ було!");
        }
        catch (Exception ex) when (ex is DomainException or ArgumentException)
        {
            Console.WriteLine($" ✗ {title}: {ex.Message.Split(Environment.NewLine)[0]}"); // без рядка «Actual value was …»
        }
    }
}

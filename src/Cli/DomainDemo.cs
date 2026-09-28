using Core.Domain;
using Core.Dto;
using Core.Import;
using Core.Services;

// Лабораторна 4: демонстрація доменної моделі (запуск: dotnet run --project src/Cli -- --domain).
internal static class DomainDemo
{
    public static int Run(string importPath)
    {
        Console.WriteLine("=== Сценарій 1: успіх ===");
        Book book = Book.Create("B-001", " 978-0-13-235088-4 ", "Чистий код", 2008, "Роберт Мартін");
        Console.WriteLine(book);

        BookCopy copy = BookCopy.Register("C-001", book.Isbn);
        Console.WriteLine(copy);

        Loan firstLoan = Loan.Open("L-001", copy, "R-001", new DateOnly(2026, 9, 1));
        Console.WriteLine(firstLoan);
        Console.WriteLine(copy);

        firstLoan.Close(new DateOnly(2026, 9, 14));
        Console.WriteLine(firstLoan);
        Console.WriteLine(copy);

        Loan secondLoan = Loan.Open("L-002", copy, "R-002", new DateOnly(2026, 9, 15));
        Console.WriteLine(secondLoan);
        Console.WriteLine();

        Console.WriteLine("=== Сценарій 2: порушення інваріантів ===");
        TryDo("повторна видача виданого примірника", () => Loan.Open("L-003", copy, "R-003", new DateOnly(2026, 9, 16)));
        TryDo("повернення примірника, що на полиці", () => BookCopy.Register("C-002", book.Isbn).Return());
        TryDo("порожній ISBN", () => BookCopy.Register("C-003", "   "));
        TryDo("ISBN неправильної довжини", () => Book.Create("B-002", "978-0-13", "Обрізаний ISBN", 2010));
        TryDo("рік видання з майбутнього", () => Book.Create("B-003", "978-0-201-61622-4", "Книга майбутнього", 2099));
        TryDo("дата повернення раніше видачі", () => secondLoan.Close(new DateOnly(2026, 9, 1)));
        TryDo("закриття вже закритої видачі", () => firstLoan.Close(new DateOnly(2026, 9, 20)));
        TryDo("порожній читач", () => Loan.Open("L-004", BookCopy.Register("C-004", book.Isbn), " ", new DateOnly(2026, 9, 16)));
        Console.WriteLine($" Стан після всіх відмов: {secondLoan}; {copy}");
        Console.WriteLine();

        Console.WriteLine("=== Сценарій 3: ToDto / FromDto ===");
        LoanDto loanDto = secondLoan.ToDto();
        Console.WriteLine($" DTO      : {loanDto}");
        Loan restored = Loan.FromDto(loanDto, copy);
        Console.WriteLine($" Відновлено: {restored}");
        TryDo("FromDto: закрита видача без дати", () => Loan.FromDto(loanDto with { Status = "Closed" }, copy));
        TryDo("FromDto: невідомий статус", () => Loan.FromDto(loanDto with { Status = "Archived" }, copy));
        TryDo("FromDto: примірник з порожнім ISBN", () => BookCopy.FromDto(new BookCopyDto("C-009", "", false)));
        Console.WriteLine();

        Console.WriteLine("=== Додаткове 1: ImportResult<BookDto> → сутності + помилки ===");
        if (File.Exists(importPath))
        {
            ImportResult<Book> books = EntityImport.ToEntities(BookCsvImporter.Load(importPath), Book.FromDto);
            Console.WriteLine($" Файл: {importPath}");
            foreach (Book b in books.Items)
                Console.WriteLine($"  + {b}");
            foreach (string error in books.Errors)
                Console.WriteLine($"  ! {error}");
            Console.WriteLine($" {books.Summary}");
        }
        else
        {
            Console.WriteLine($" Файл не знайдено: {Path.GetFullPath(importPath)}");
        }
        Console.WriteLine();

        Console.WriteLine($"=== Додаткове 2: не більше {LendingService.MaxActiveLoansPerReader} незакритих видач на читача ===");
        var desk = new LendingService();
        var day = new DateOnly(2026, 9, 1);
        for (int i = 1; i <= LendingService.MaxActiveLoansPerReader; i++)
            desk.IssueCopy(BookCopy.Register($"C-10{i}", book.Isbn), "R-007", day);
        Console.WriteLine($" Читач R-007 має незакритих видач: {desk.CountActiveLoans("R-007")}");
        BookCopy sixth = BookCopy.Register("C-106", book.Isbn);
        TryDo("шоста видача", () => desk.IssueCopy(sixth, "R-007", day));
        Console.WriteLine($" Примірник {sixth.Id} після відмови: {(sixth.IsIssued ? "виданий" : "на полиці")}");
        desk.ReturnCopy("L-001", day.AddDays(7));
        Console.WriteLine($" Після повернення L-001: {desk.IssueCopy(sixth, "R-007", day.AddDays(7))}");
        Console.WriteLine();

        Console.WriteLine("=== Додаткове 3: переходи стану Open → Lost → Closed ===");
        Loan lostLoan = desk.Loans[1];
        lostLoan.MarkLost();
        Console.WriteLine($" {lostLoan}");
        TryDo("повторна позначка «втрачено»", lostLoan.MarkLost);
        lostLoan.Close(day.AddDays(30));
        Console.WriteLine($" {lostLoan} (примірник знайшовся)");
        TryDo("втрата вже закритої видачі", lostLoan.MarkLost);

        return 0;
    }

    private static void TryDo(string title, Action action)
    {
        try
        {
            action();
            Console.WriteLine($" {title}: виняток НЕ спрацював — інваріант відсутній!");
        }
        catch (Exception ex)
        {
            Console.WriteLine($" {title}: {ex.GetType().Name} — {ex.Message}");
        }
    }
}

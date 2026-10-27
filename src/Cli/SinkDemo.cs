using Core;
using Core.Domain;
using Core.Services;

// Лабораторна 6, додаткове завдання 1 (запуск: dotnet run --project src/Cli -- --sinks [--file]).
// Сервіс отримав INotificationSink через конструктор і викликає його сам; на подію тут ніхто не підписаний.
internal static class SinkDemo
{
    public static int Run(LendingService service, string sinkLogPath)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        Console.WriteLine("=== Дод. 1: сповіщення через INotificationSink (ConsoleSink + FileSink) ===");
        Book book = service.FindByIsbn("978-1-4919-5038-8") ?? service.AddBook("978-1-4919-5038-8", "C# 7.0 in a Nutshell", 2017, "Джозеф Албахарі");
        BookCopy copy = service.RegisterCopy(book.Isbn);
        Loan loan = service.IssueCopy(copy.Id, "R-020", today);
        service.ReturnCopy(loan.Id, today);
        Console.WriteLine();

        Console.WriteLine("=== Відмова: виняток є, сповіщення немає ===");
        try
        {
            service.ReturnCopy(loan.Id, today); // видача вже закрита — Loan.Close відмовить
        }
        catch (DomainException ex)
        {
            Console.WriteLine($" ✗ {ex.Message}");
        }
        Console.WriteLine();

        Console.WriteLine($"Файл FileSink: {sinkLogPath}");
        Console.WriteLine("«Відписатися» від sink неможливо: одержувач заданий у конструкторі на весь час життя сервісу.");
        return 0;
    }
}

using Core.Domain;
using Core.Services;

// Лабораторна 7, крок 3: відкладене виконання LINQ (запуск: dotnet run --project src/Cli -- report-deferred).
// Тут Where навмисно стоїть у Cli: це не звіт, а демонстрація того, коли саме перебирається джерело.
internal static class DeferredDemo
{
    public static void Run(LendingService service, DateOnly today)
    {
        // service.All<Loan>() щоразу повертає новий знімок сховища, тож для демо беремо локальний список.
        List<Loan> source = service.All<Loan>().ToList();
        int checks = 0; // скільки разів викликано предикат — видно, коли запит справді виконується

        IEnumerable<Loan> query = source.Where(l => { checks++; return l.IsActive && l.DueOn < today; });
        Console.WriteLine($" Запит побудовано; предикат викликано {checks} разів (джерело: {source.Count} видач)");

        Console.WriteLine($" Прострочених до змін:    {query.Count()}   (перевірок предиката: {checks})");

        BookCopy copy = BookCopy.Register("C-999", "978-0-201-61622-4");
        source.Add(Loan.Open("L-999", copy, "R-099", today.AddDays(-30))); // ще одна прострочена видача
        Console.WriteLine($" Прострочених після змін: {query.Count()}   (перевірок: {checks} — запит виконано вдруге, з новим елементом)");

        List<Loan> snapshot = query.ToList(); // результат зафіксовано
        source.Clear();
        Console.WriteLine($" Після source.Clear(): query.Count() = {query.Count()}, snapshot.Count = {snapshot.Count} (знімок не змінюється)");
        Console.WriteLine();

        // Потокові оператори передають елементи по одному; буферизувальні спершу читають усе джерело.
        var trace = new List<string>();
        int[] ids = [1, 2, 3];
        foreach (int _ in ids.Where(i => { trace.Add($"W{i}"); return true; }).Select(i => { trace.Add($"S{i}"); return i; })) { }
        Console.WriteLine($" Where → Select   (потокові):       {string.Join(" ", trace)}");

        trace.Clear();
        foreach (int _ in ids.Where(i => { trace.Add($"W{i}"); return true; }).OrderBy(i => -i).Select(i => { trace.Add($"S{i}"); return i; })) { }
        Console.WriteLine($" Where → OrderBy → Select (буфер):  {string.Join(" ", trace)}");
        Console.WriteLine();

        // Зміна джерела під час перебирання відкладеного запиту.
        var list = service.All<Loan>().ToList();
        try
        {
            foreach (Loan l in list.Where(l => l.IsActive))
                list.Remove(l);
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($" ✗ зміна джерела всередині foreach: {ex.Message}");
        }
        list = service.All<Loan>().ToList(); // перша спроба встигла вилучити один елемент — беремо свіжий знімок
        int removed = 0;
        foreach (Loan l in list.Where(l => l.IsActive).ToList()) // матеріалізовано до змін — працює
            removed += list.Remove(l) ? 1 : 0;
        Console.WriteLine($" ✓ з ToList() перед циклом вилучено {removed} незакритих видач, лишилось {list.Count}");
    }
}

using System.Diagnostics;
using Core;
using Core.Reports;

// Лабораторна 7, дод. завдання 4: час звіту LowStock на згенерованих даних (запуск: report-bench [кількість]).
// Варіанти запиту — у Core/Reports/ReportBenchmarks.cs; тут лише генерація, Stopwatch і вивід.
internal static class BenchDemo
{
    private const int Runs = 10;
    private const int Threshold = 1; // видання без жодного примірника на полиці
#if DEBUG
    private const string Configuration = "Debug (вимірювати краще з -c Release)";
#else
    private const string Configuration = "Release";
#endif

    public static void Run(int count)
    {
        var sw = Stopwatch.StartNew();
        var (books, copies) = SampleData.Generate(count);
        Console.WriteLine($" Згенеровано {books.Count:N0} видань і {copies.Count:N0} примірників за {sw.ElapsedMilliseconds} мс; " +
                          $"кожен варіант — {Runs} запусків після прогріву, збірка {Configuration}");
        Console.WriteLine();

        Measure("LowStock: Where → OrderBy → ToList (як у ReportService)", () => ReportBenchmarks.Streamed(books, copies, Threshold).Count);
        Measure("ToList після кожного кроку ланцюжка", () => ReportBenchmarks.ToListEachStep(books, copies, Threshold).Count);
        Measure("OrderBy → Where (сортуються всі видання)", () => ReportBenchmarks.SortThenFilter(books, copies, Threshold).Count);
        Measure("відкладений запит: Count() + ToList() — 2 проходи", () => ReportBenchmarks.DeferredTwice(books, copies, Threshold) / 2);
        Measure("матеріалізовано один раз: ToList(), потім .Count", () => ReportBenchmarks.MaterializedOnce(books, copies, Threshold) / 2);
    }

    private static void Measure(string title, Func<int> report)
    {
        int rows = report(); // прогрів: JIT і перший прохід не входять у вимір
        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        var times = new List<double>(Runs);
        for (int i = 0; i < Runs; i++)
        {
            var sw = Stopwatch.StartNew();
            rows = report();
            times.Add(sw.Elapsed.TotalMilliseconds);
        }
        double mbPerRun = (GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore) / (double)Runs / 1024 / 1024;
        times.Sort();
        Console.WriteLine($" {title,-55} медіана {times[Runs / 2],8:F1} мс  мін {times[0],8:F1} мс  " +
                          $"алок. {mbPerRun,6:F1} МБ  рядків {rows:N0}");
    }
}

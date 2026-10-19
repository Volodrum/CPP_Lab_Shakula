using Core.Domain;

namespace Core.Reports;

// Дод. завдання 4: варіанти одного звіту (LowStock) для вимірювання Stopwatch у Cli.
// Усі повертають той самий результат — різниться лише те, скільки разів і в якому порядку перебирається джерело.
public static class ReportBenchmarks
{
    // Як у ReportService.LowStock: фільтр → сортування → один ToList наприкінці.
    public static IReadOnlyList<StockLine> Streamed(IEnumerable<Book> books, IEnumerable<BookCopy> copies, int threshold) =>
        ReportService.LowStock(books, copies, threshold);

    // ToList після кожного кроку: кожен проміжний результат копіюється в новий список.
    public static IReadOnlyList<StockLine> ToListEachStep(IEnumerable<Book> books, IEnumerable<BookCopy> copies, int threshold) =>
        ReportService.StockQuery(books.ToList(), copies.ToList()).ToList()
            .Where(s => s.OnShelf < threshold).ToList()
            .OrderBy(s => s.OnShelf).ThenBy(s => s.BookId, StringComparer.Ordinal).ToList();

    // Типова помилка: Where після OrderBy — сортуються всі видання, а не лише відфільтровані.
    public static IReadOnlyList<StockLine> SortThenFilter(IEnumerable<Book> books, IEnumerable<BookCopy> copies, int threshold) =>
        ReportService.StockQuery(books, copies)
            .OrderBy(s => s.OnShelf).ThenBy(s => s.BookId, StringComparer.Ordinal)
            .Where(s => s.OnShelf < threshold)
            .ToList();

    // Відкладений запит перебрано двічі (Count() + ToList()): GroupJoin і фільтр виконуються двічі.
    public static int DeferredTwice(IEnumerable<Book> books, IEnumerable<BookCopy> copies, int threshold)
    {
        IEnumerable<StockLine> query = ReportService.StockQuery(books, copies).Where(s => s.OnShelf < threshold);
        int count = query.Count();
        List<StockLine> lines = query.ToList();
        return count + lines.Count;
    }

    // Те саме, але матеріалізовано один раз: Count читає властивість списку.
    public static int MaterializedOnce(IEnumerable<Book> books, IEnumerable<BookCopy> copies, int threshold)
    {
        List<StockLine> lines = ReportService.StockQuery(books, copies).Where(s => s.OnShelf < threshold).ToList();
        return lines.Count + lines.Count;
    }
}

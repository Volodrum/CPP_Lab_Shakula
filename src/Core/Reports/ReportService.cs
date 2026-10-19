using Core.Domain;

namespace Core.Reports;

// Тиждень 7: аналітичні запити LINQ to Objects. Чисті функції: приймають IEnumerable<T> (будь-яке джерело —
// сховище, файл, тестовий масив), повертають IReadOnlyList<DTO>, не знають ні про консоль, ні про сховище.
//
// Коли виконується запит. Where/Select/OrderBy/GroupBy/GroupJoin лише описують ланцюжок; джерело перебирається
// в момент ToList() наприкінці кожного методу (у Totals — у ToList() на початку). Тому результат кожного звіту —
// готовий знімок: повторний Count чи foreach у Cli не виконує запит удруге, а зміна сховища після виклику
// звіту на вже отриманий список не впливає. Демо з двома різними Count() — у Cli/DeferredDemo.cs.
public static class ReportService
{
    // 1) Where + OrderBy/ThenBy + проєкція у DTO: які видачі прострочені на дату today?
    public static IReadOnlyList<LoanLine> Overdue(IEnumerable<Loan> loans, DateOnly today) =>
        loans.Where(l => l.IsActive && l.DueOn < today)
            .OrderBy(l => l.DueOn).ThenBy(l => l.Id, StringComparer.Ordinal)
            .Select(l => new LoanLine(l.Id, l.Copy.Id, l.ReaderId, l.IssuedOn, l.DueOn, today.DayNumber - l.DueOn.DayNumber))
            .ToList();

    // 2) GroupBy + топ-N зі стабільним порядком: хто з читачів брав книги найчастіше?
    public static IReadOnlyList<ReaderStat> TopReaders(IEnumerable<Loan> loans, int n) =>
        loans.GroupBy(l => l.ReaderId, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ReaderStat(g.Key, g.Count(), g.Count(l => l.IsActive)))
            .TopBy(r => r.Loans, n, r => r.ReaderId) // = OrderByDescending(Loans).ThenBy(ReaderId, Ordinal).Take(n)
            .ToList();

    // 3) GroupBy за значенням (десятиліття) з підрахунком і сумою: як розподілено фонд за роками видання?
    public static IReadOnlyList<GroupSummary> ByDecade(IEnumerable<Book> books, IEnumerable<BookCopy> copies) =>
        StockQuery(books, copies)
            .GroupBy(s => s.Year / 10 * 10)
            .Select(g => new GroupSummary($"{g.Key}-ті", g.Count(), g.Sum(s => s.Copies)))
            .OrderByDescending(s => s.TotalCopies).ThenBy(s => s.Key, StringComparer.Ordinal)
            .ToList();

    // 4) Агрегати з обробкою порожньої послідовності: скільки видань і примірників, середнє і максимум на видання.
    public static (int Books, int Copies, double AverageCopies, int MaxCopies) Totals(
        IEnumerable<Book> books, IEnumerable<BookCopy> copies)
    {
        var stock = StockQuery(books, copies).ToList(); // один прохід; далі чотири агрегати по готовому списку
        return stock.Count == 0
            ? (0, 0, 0d, 0) // Average і Max на порожньому списку кинули б InvalidOperationException
            : (stock.Count, stock.Sum(s => s.Copies), stock.Average(s => s.Copies), stock.Max(s => s.Copies));
    }

    // 5) Фільтр за обчисленим полем: яких видань на полиці менше threshold примірників?
    public static IReadOnlyList<StockLine> LowStock(IEnumerable<Book> books, IEnumerable<BookCopy> copies, int threshold) =>
        StockQuery(books, copies)
            .Where(s => s.OnShelf < threshold)
            .OrderBy(s => s.OnShelf).ThenBy(s => s.BookId, StringComparer.Ordinal)
            .ToList();

    // 6) Топ-N видань за кількістю примірників (друге використання TopBy).
    public static IReadOnlyList<StockLine> TopBooks(IEnumerable<Book> books, IEnumerable<BookCopy> copies, int n) =>
        StockQuery(books, copies)
            .TopBy(s => s.Copies, n, s => s.BookId)
            .ToList();

    // Дод. завдання 1 (Join): прострочені видачі разом з назвою видання. Join лишає лише пари зі збігом ключа.
    public static IReadOnlyList<OverdueLine> OverdueWithTitles(IEnumerable<Loan> loans, IEnumerable<Book> books, DateOnly today) =>
        loans.Where(l => l.IsActive && l.DueOn < today) // фільтр до з'єднання: з'єднуємо менше пар
            .Join(books,
                loan => IsbnRules.Compact(loan.Copy.Isbn),
                book => IsbnRules.Compact(book.Isbn),
                (loan, book) => new OverdueLine(loan.Id, loan.ReaderId, book.Isbn, book.Title, loan.DueOn,
                    today.DayNumber - loan.DueOn.DayNumber))
            .OrderByDescending(o => o.DaysOverdue).ThenBy(o => o.LoanId, StringComparer.Ordinal)
            .ToList();

    // Дод. завдання 3: той самий звіт, що й ByDecade, у синтаксисі запитів. Компілятор перекладає його
    // на ті самі виклики GroupBy / OrderByDescending / ThenBy / Select — результат збігається поелементно.
    public static IReadOnlyList<GroupSummary> ByDecadeQuery(IEnumerable<Book> books, IEnumerable<BookCopy> copies) =>
        (from s in StockQuery(books, copies)
         group s by s.Year / 10 * 10 into g
         let total = g.Sum(s => s.Copies)
         orderby total descending, g.Key // int-ключ: 1980 < 1990 так само, як Ordinal для "1980-ті" < "1990-ті"
         select new GroupSummary($"{g.Key}-ті", g.Count(), total))
        .ToList();

    // Фонд по виданнях як відкладений запит: GroupJoin (дод. завдання 1) зберігає й видання без жодного примірника,
    // на відміну від Join. Ключ — ISBN без дефісів: «978-0-13-…» і «978013…» — одне видання.
    internal static IEnumerable<StockLine> StockQuery(IEnumerable<Book> books, IEnumerable<BookCopy> copies) =>
        books.GroupJoin(copies,
            book => IsbnRules.Compact(book.Isbn),
            copy => IsbnRules.Compact(copy.Isbn),
            (book, bookCopies) => new StockLine(book.Id, book.Isbn, book.Title, book.Year,
                bookCopies.Count(), bookCopies.Count(c => !c.IsIssued)));
}

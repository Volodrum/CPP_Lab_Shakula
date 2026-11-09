using System.Globalization;
using Core.Abstractions;
using Core.Domain;

namespace Core.Services;

// Тиждень 10: CRUD каталогу для HTTP API. Сервіс не знає ні про ASP.NET, ні про JSON: приймає BookDraft,
// повертає CatalogEntry (видання + кількість примірників), порушення правил бібліотеки — DomainException.
// Як саме це стане 201/404/409, вирішує Api.
//
// «Видання» в API — це Book разом з його примірниками: поле copies у запиті задає, скільки примірників
// має бути. POST реєструє їх, PUT додає нові або вилучає вільні, DELETE прибирає видання з примірниками.
// Примірник, який зараз видано або має історію видач, не вилучається — це 409, а не тихе знищення даних.
//
// Методи асинхронні й приймають CancellationToken (контракт тижня 8), хоча IBookStore синхронний:
// на тижні 11 сховище стане EF Core, а сигнатури, які бачить Api, лишаться тими самими.
public sealed class CatalogService(IBookStore store)
{
    public const int MaxCopies = 100;
    public const int MaxPageSize = 100;

    private static readonly StringComparer TitleOrder =
        StringComparer.Create(CultureInfo.GetCultureInfo("uk-UA"), ignoreCase: true); // «і», «ї», «є» — на своїх місцях

    private readonly IBookStore _store = store ?? throw new ArgumentNullException(nameof(store));

    // Одна зміна каталогу за раз. lock сховища захищає кожен окремий виклик, а тут треба атомарно
    // «перевірити ISBN → додати книгу → додати примірники». SemaphoreSlim, бо чекати його можна через await.
    // Працює лише тоді, коли сервіс один на весь застосунок (Singleton у DI).
    private readonly SemaphoreSlim _writeGate = new(1, 1);

    public Task<CatalogPage> SearchAsync(CatalogQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Page, 1, nameof(query.Page));
        if (query.PageSize is < 1 or > MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(query), query.PageSize, $"Розмір сторінки має бути 1..{MaxPageSize}");
        ct.ThrowIfCancellationRequested();

        IEnumerable<Book> books = _store.List<Book>();

        if (!string.IsNullOrWhiteSpace(query.Author))
        {
            string author = query.Author.Trim();
            books = books.Where(b => b.Author?.Contains(author, StringComparison.OrdinalIgnoreCase) == true);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            string term = query.Search.Trim();
            string isbnTerm = IsbnRules.Compact(term);
            books = books.Where(b =>
                b.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
                || b.Author?.Contains(term, StringComparison.OrdinalIgnoreCase) == true
                || (isbnTerm.Length > 0 && IsbnRules.Compact(b.Isbn).Contains(isbnTerm, StringComparison.Ordinal)));
        }

        // Стабільний порядок — інакше сторінки 1 і 2 могли б перетнутися.
        List<Book> matched = books.OrderBy(b => b.Title, TitleOrder).ThenBy(b => b.Id, StringComparer.Ordinal).ToList();

        long skip = (long)(query.Page - 1) * query.PageSize; // page=int.MaxValue не переповнить int
        var copies = CopiesByIsbn();
        List<CatalogEntry> items = skip >= matched.Count
            ? []
            : matched.Skip((int)skip).Take(query.PageSize).Select(b => Entry(b, copies[IsbnRules.Compact(b.Isbn)])).ToList();

        return Task.FromResult(new CatalogPage(items, matched.Count, query.Page, query.PageSize));
    }

    public Task<CatalogEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Book? book = string.IsNullOrWhiteSpace(id) ? null : _store.GetById<Book>(id);
        return Task.FromResult(book is null ? null : Entry(book));
    }

    public async Task<CatalogEntry> CreateAsync(BookDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        EnsureCopies(draft.Copies);
        Book book = Book.Create(NewId("B"), draft.Isbn, draft.Title, draft.Year, draft.Author); // інваріанти — у домені

        await _writeGate.WaitAsync(ct); // скасувати можна лише очікування; почату зміну доводимо до кінця
        try
        {
            EnsureIsbnFree(book.Isbn, exceptId: null);

            _store.Add(book);
            for (int i = 0; i < draft.Copies; i++)
                _store.Add(BookCopy.Register(NewId("C"), book.Isbn));

            return Entry(book);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    // PUT: повна заміна. null — видання немає (404). Book незмінний, тож створюємо новий з тим самим id.
    public async Task<CatalogEntry?> ReplaceAsync(string id, BookDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        EnsureCopies(draft.Copies);

        await _writeGate.WaitAsync(ct);
        try
        {
            Book? current = string.IsNullOrWhiteSpace(id) ? null : _store.GetById<Book>(id);
            if (current is null) return null;

            Book replacement = Book.Create(current.Id, draft.Isbn, draft.Title, draft.Year, draft.Author);
            List<BookCopy> copies = CopiesOf(current.Isbn);

            bool isbnChanged = IsbnRules.Compact(current.Isbn) != IsbnRules.Compact(replacement.Isbn);
            if (isbnChanged)
            {
                EnsureIsbnFree(replacement.Isbn, exceptId: current.Id);
                if (copies.Count > 0)
                    throw new DomainException(
                        $"Видання {current.Id} має {copies.Count} примірн. з ISBN {current.Isbn}: " +
                        "змінити ISBN можна лише виданню без примірників.");
            }

            // Спершу з'ясовуємо, чи зміна можлива взагалі, і лише потім змінюємо сховище: 409 не лишає півзміни.
            List<BookCopy> toRemove = [];
            int surplus = copies.Count - draft.Copies;
            if (surplus > 0)
            {
                toRemove = Removable(copies).Take(surplus).ToList();
                if (toRemove.Count < surplus)
                    throw new DomainException(
                        $"Видання {current.Id}: вилучити можна лише {toRemove.Count} з {surplus} зайвих примірн. — " +
                        "решту видано читачам або вони мають історію видач.");
            }

            _store.Update(replacement);
            foreach (BookCopy copy in toRemove)
                _store.Remove(copy.Id);
            for (int i = copies.Count; i < draft.Copies; i++)
                _store.Add(BookCopy.Register(NewId("C"), replacement.Isbn));

            return Entry(replacement);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    // false — видання не було (404). Видання з виданими примірниками чи історією видач — DomainException (409).
    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        await _writeGate.WaitAsync(ct);
        try
        {
            Book? book = string.IsNullOrWhiteSpace(id) ? null : _store.GetById<Book>(id);
            if (book is null) return false;

            List<BookCopy> copies = CopiesOf(book.Isbn);
            List<BookCopy> removable = Removable(copies).ToList();
            if (removable.Count < copies.Count)
                throw new DomainException(
                    $"Видання {book.Id} не можна вилучити: {copies.Count - removable.Count} з {copies.Count} примірн. " +
                    "видано читачам або мають історію видач.");

            foreach (BookCopy copy in copies)
                _store.Remove(copy.Id);
            return _store.Remove(book.Id);
        }
        finally
        {
            _writeGate.Release();
        }
    }

    private void EnsureIsbnFree(string isbn, string? exceptId)
    {
        Book? existing = _store.List<Book>().FirstOrDefault(b => b.Id != exceptId && b.HasIsbn(isbn));
        if (existing is not null)
            throw new DomainException($"Видання з ISBN {isbn} уже є в каталозі: {existing.Id}.");
    }

    // Примірник можна вилучити, якщо він на полиці і жодна видача (навіть закрита) на нього не посилається:
    // інакше файл сховища містив би видачу без примірника і не відкрився б наступного разу.
    private IEnumerable<BookCopy> Removable(IEnumerable<BookCopy> copies)
    {
        var referenced = _store.List<Loan>().Select(l => l.Copy.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return copies.Where(c => !c.IsIssued && !referenced.Contains(c.Id));
    }

    private List<BookCopy> CopiesOf(string isbn)
    {
        string key = IsbnRules.Compact(isbn);
        return _store.List<BookCopy>().Where(c => IsbnRules.Compact(c.Isbn) == key).ToList();
    }

    private ILookup<string, BookCopy> CopiesByIsbn() => _store.List<BookCopy>().ToLookup(c => IsbnRules.Compact(c.Isbn));

    private CatalogEntry Entry(Book book) => Entry(book, CopiesOf(book.Isbn));

    private static CatalogEntry Entry(Book book, IEnumerable<BookCopy> copies)
    {
        var list = copies as ICollection<BookCopy> ?? copies.ToList();
        return new CatalogEntry(book, list.Count, list.Count(c => !c.IsIssued));
    }

    private static void EnsureCopies(int copies)
    {
        if (copies is < 0 or > MaxCopies)
            throw new ArgumentOutOfRangeException(nameof(copies), copies, $"Кількість примірників має бути 0..{MaxCopies}");
    }

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}

// Що клієнт хоче мати в каталозі: поля видання + бажана кількість примірників.
public sealed record BookDraft(string Isbn, string Title, int Year, string? Author, int Copies);

// Видання з підсумком по примірниках. Book цих чисел не знає — вони з'являються лише тут (як StockLine тижня 7).
public sealed record CatalogEntry(Book Book, int Copies, int OnShelf);

public sealed record CatalogQuery(string? Author = null, string? Search = null, int Page = 1, int PageSize = 20);

public sealed record CatalogPage(IReadOnlyList<CatalogEntry> Items, int Total, int Page, int PageSize);

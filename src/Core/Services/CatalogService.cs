using System.Globalization;
using Core.Abstractions;
using Core.Domain;
using Core.Storage;

namespace Core.Services;

// Тиждень 10: CRUD каталогу для HTTP API. Сервіс не знає ні про ASP.NET, ні про JSON: приймає BookDraft,
// повертає CatalogEntry (видання + кількість примірників), порушення правил бібліотеки — DomainException.
// Як саме це стане 201/404/409, вирішує Api.
//
// «Видання» в API — це Book разом з його примірниками: поле copies у запиті задає, скільки примірників
// має бути. POST реєструє їх, PUT додає нові або вилучає вільні, DELETE прибирає видання з примірниками.
// Примірник, який зараз видано або має історію видач, не вилучається — це 409, а не тихе знищення даних.
//
// Тиждень 11: сховище за IBookStoreAsync (дод. завдання 1) — для EF Core це ToListAsync / SaveChangesAsync.
// Кожна зміна — в InTransactionAsync: «видання + N примірників» потрапляє в БД цілком або не потрапляє взагалі.
// Сервіс тепер Scoped (як DbContext під ним), а замок «одна зміна за раз» — спільний CatalogWriteGate.
// Сигнатури, які бачить Api, не змінилися з тижня 10.
public sealed class CatalogService
{
    public const int MaxCopies = 100;
    public const int MaxPageSize = 100;

    private static readonly StringComparer TitleOrder =
        StringComparer.Create(CultureInfo.GetCultureInfo("uk-UA"), ignoreCase: true); // «і», «ї», «є» — на своїх місцях

    private readonly IBookStoreAsync _store;

    // Одна зміна каталогу за раз: «перевірити ISBN → додати книгу → додати примірники» не перемежовується з іншою.
    // Без переданого gate кожен сервіс має власний (тести Core, Cli); Api передає один Singleton на всіх.
    private readonly CatalogWriteGate _writeGate;

    public CatalogService(IBookStoreAsync store, CatalogWriteGate? writeGate = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _writeGate = writeGate ?? new CatalogWriteGate();
    }

    // Синхронне сховище тижнів 5–10 (InMemory, File) — через адаптер, щоб старі виклики не змінювати.
    public CatalogService(IBookStore store) : this((store ?? throw new ArgumentNullException(nameof(store))).AsAsync())
    {
    }

    public async Task<CatalogPage> SearchAsync(CatalogQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Page, 1, nameof(query.Page));
        if (query.PageSize is < 1 or > MaxPageSize)
            throw new ArgumentOutOfRangeException(nameof(query), query.PageSize, $"Розмір сторінки має бути 1..{MaxPageSize}");
        ct.ThrowIfCancellationRequested();

        IEnumerable<Book> books = await _store.ListAsync<Book>(ct);

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
        var copies = await CopiesByIsbnAsync(ct);
        List<CatalogEntry> items = skip >= matched.Count
            ? []
            : matched.Skip((int)skip).Take(query.PageSize).Select(b => Entry(b, copies[IsbnRules.Compact(b.Isbn)])).ToList();

        return new CatalogPage(items, matched.Count, query.Page, query.PageSize);
    }

    public async Task<CatalogEntry?> GetAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        Book? book = string.IsNullOrWhiteSpace(id) ? null : await _store.GetByIdAsync<Book>(id, ct);
        return book is null ? null : await EntryAsync(book, ct);
    }

    public async Task<CatalogEntry> CreateAsync(BookDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        EnsureCopies(draft.Copies);
        Book book = Book.Create(NewId("B"), draft.Isbn, draft.Title, draft.Year, draft.Author); // інваріанти — у домені

        using (await _writeGate.EnterAsync(ct))
        {
            return await _store.InTransactionAsync(async tct =>
            {
                await EnsureIsbnFreeAsync(book.Isbn, exceptId: null, tct);

                await _store.AddAsync(book, tct);
                for (int i = 0; i < draft.Copies; i++)
                    await _store.AddAsync(BookCopy.Register(NewId("C"), book.Isbn), tct);

                return await EntryAsync(book, tct);
            }, ct);
        }
    }

    // PUT: повна заміна. null — видання немає (404). Book незмінний, тож створюємо новий з тим самим id.
    public async Task<CatalogEntry?> ReplaceAsync(string id, BookDraft draft, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        EnsureCopies(draft.Copies);

        using (await _writeGate.EnterAsync(ct))
        {
            return await _store.InTransactionAsync<CatalogEntry?>(async tct =>
            {
                Book? current = string.IsNullOrWhiteSpace(id) ? null : await _store.GetByIdAsync<Book>(id, tct);
                if (current is null) return null;

                Book replacement = Book.Create(current.Id, draft.Isbn, draft.Title, draft.Year, draft.Author);
                List<BookCopy> copies = await CopiesOfAsync(current.Isbn, tct);

                bool isbnChanged = IsbnRules.Compact(current.Isbn) != IsbnRules.Compact(replacement.Isbn);
                if (isbnChanged)
                {
                    await EnsureIsbnFreeAsync(replacement.Isbn, exceptId: current.Id, tct);
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
                    toRemove = (await RemovableAsync(copies, tct)).Take(surplus).ToList();
                    if (toRemove.Count < surplus)
                        throw new DomainException(
                            $"Видання {current.Id}: вилучити можна лише {toRemove.Count} з {surplus} зайвих примірн. — " +
                            "решту видано читачам або вони мають історію видач.");
                }

                await _store.UpdateAsync(replacement, tct);
                foreach (BookCopy copy in toRemove)
                    await _store.RemoveAsync(copy.Id, tct);
                for (int i = copies.Count; i < draft.Copies; i++)
                    await _store.AddAsync(BookCopy.Register(NewId("C"), replacement.Isbn), tct);

                return await EntryAsync(replacement, tct);
            }, ct);
        }
    }

    // false — видання не було (404). Видання з виданими примірниками чи історією видач — DomainException (409).
    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        using (await _writeGate.EnterAsync(ct))
        {
            return await _store.InTransactionAsync(async tct =>
            {
                Book? book = string.IsNullOrWhiteSpace(id) ? null : await _store.GetByIdAsync<Book>(id, tct);
                if (book is null) return false;

                List<BookCopy> copies = await CopiesOfAsync(book.Isbn, tct);
                List<BookCopy> removable = (await RemovableAsync(copies, tct)).ToList();
                if (removable.Count < copies.Count)
                    throw new DomainException(
                        $"Видання {book.Id} не можна вилучити: {copies.Count - removable.Count} з {copies.Count} примірн. " +
                        "видано читачам або мають історію видач.");

                foreach (BookCopy copy in copies)
                    await _store.RemoveAsync(copy.Id, tct);
                return await _store.RemoveAsync(book.Id, tct);
            }, ct);
        }
    }

    private async Task EnsureIsbnFreeAsync(string isbn, string? exceptId, CancellationToken ct)
    {
        Book? existing = (await _store.ListAsync<Book>(ct)).FirstOrDefault(b => b.Id != exceptId && b.HasIsbn(isbn));
        if (existing is not null)
            throw new DomainException($"Видання з ISBN {isbn} уже є в каталозі: {existing.Id}.");
    }

    // Примірник можна вилучити, якщо він на полиці і жодна видача (навіть закрита) на нього не посилається:
    // інакше сховище містило б видачу без примірника (у БД це ще й порушення зовнішнього ключа Loans → Copies).
    private async Task<IEnumerable<BookCopy>> RemovableAsync(IEnumerable<BookCopy> copies, CancellationToken ct)
    {
        var referenced = (await _store.ListAsync<Loan>(ct)).Select(l => l.Copy.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return copies.Where(c => !c.IsIssued && !referenced.Contains(c.Id));
    }

    private async Task<List<BookCopy>> CopiesOfAsync(string isbn, CancellationToken ct)
    {
        string key = IsbnRules.Compact(isbn);
        return (await _store.ListAsync<BookCopy>(ct)).Where(c => IsbnRules.Compact(c.Isbn) == key).ToList();
    }

    private async Task<ILookup<string, BookCopy>> CopiesByIsbnAsync(CancellationToken ct) =>
        (await _store.ListAsync<BookCopy>(ct)).ToLookup(c => IsbnRules.Compact(c.Isbn));

    private async Task<CatalogEntry> EntryAsync(Book book, CancellationToken ct) =>
        Entry(book, await CopiesOfAsync(book.Isbn, ct));

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

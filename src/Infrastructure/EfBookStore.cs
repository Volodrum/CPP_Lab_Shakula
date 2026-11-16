using System.Linq.Expressions;
using Core;
using Core.Abstractions;
using Core.Domain;
using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Infrastructure;

// Тиждень 11: сховище над EF Core + SQLite за НЕЗМІНЕНИМ контрактом Core.Abstractions.IBookStore (тиждень 5),
// плюс асинхронний IBookStoreAsync (дод. завдання 1). Core про цей клас не знає — його обирає лише Program.cs Api.
//
// Читання — AsNoTracking: рядки потрібні лише щоб зібрати доменний об'єкт, відстежувати їхні зміни нема чого.
// Запис — завантажили рядок (з відстеженням) → змінили поля → SaveChanges сформував UPDATE лише змінених стовпців.
// Row → домен — через фабрики (Book.Create, BookCopy.FromDto, Loan.FromDto) у RowMapping.
//
// Синхронні й асинхронні методи мають одну реалізацію *Core(bool async, …): при async=false кожен await отримує
// вже завершений Task (ToList замість ToListAsync), тож .GetAwaiter().GetResult() не блокує потік і не дає deadlock.
// Той самий прийом використовують усередині EF Core і HttpClient.
public sealed class EfBookStore(AppDbContext db, TimeProvider? clock = null) : IBookStore, IBookStoreAsync
{
    private readonly AppDbContext _db = db ?? throw new ArgumentNullException(nameof(db));
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    // ---------- IBookStore (синхронний, тиждень 5)

    public IReadOnlyList<T> List<T>() where T : class, ILibraryEntity =>
        ListCore<T>(async: false, CancellationToken.None).GetAwaiter().GetResult();

    public T? GetById<T>(string id) where T : class, ILibraryEntity =>
        GetByIdCore<T>(id, async: false, CancellationToken.None).GetAwaiter().GetResult();

    public void Add(ILibraryEntity item) => AddCore(item, async: false, CancellationToken.None).GetAwaiter().GetResult();

    public void Update(ILibraryEntity item) => UpdateCore(item, async: false, CancellationToken.None).GetAwaiter().GetResult();

    public bool Remove(string id) => RemoveCore(id, async: false, CancellationToken.None).GetAwaiter().GetResult();

    // ---------- IBookStoreAsync (дод. завдання 1)

    public Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken ct = default) where T : class, ILibraryEntity =>
        ListCore<T>(async: true, ct);

    public Task<T?> GetByIdAsync<T>(string id, CancellationToken ct = default) where T : class, ILibraryEntity =>
        GetByIdCore<T>(id, async: true, ct);

    public Task AddAsync(ILibraryEntity item, CancellationToken ct = default) => AddCore(item, async: true, ct);

    public Task UpdateAsync(ILibraryEntity item, CancellationToken ct = default) => UpdateCore(item, async: true, ct);

    public Task<bool> RemoveAsync(string id, CancellationToken ct = default) => RemoveCore(id, async: true, ct);

    // Кілька SaveChanges усередині — одна транзакція SQLite: або все, або нічого.
    // Вкладений виклик (транзакція вже відкрита) просто приєднується до зовнішньої.
    public async Task<TResult> InTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        if (_db.Database.CurrentTransaction is not null)
            return await work(ct);

        await using IDbContextTransaction tx = await _db.Database.BeginTransactionAsync(ct);
        try
        {
            TResult result = await work(ct);
            await tx.CommitAsync(CancellationToken.None); // роботу виконано — фіксуємо, навіть якщо клієнт уже пішов
            return result;
        }
        catch
        {
            // DisposeAsync відкотить транзакцію; невдалі Added/Modified-рядки прибираємо з трекера,
            // щоб наступний SaveChanges у цьому ж контексті не спробував записати їх ще раз.
            _db.ChangeTracker.Clear();
            throw;
        }
    }

    public override string ToString() => $"{nameof(EfBookStore)} (SQLite: {_db.Database.GetDbConnection().DataSource})";

    // ---------- Читання

    // LINQ to Entities: Where / OrderBy / Select нижче перекладаються в SQL і виконуються в SQLite.
    // Лише після ToList (матеріалізації) працює LINQ to Objects — RowMapping.ToDomain з перевірками домену.
    private static readonly Expression<Func<CopyRow, CopyView>> CopyProjection =
        c => new CopyView(c.Id, c.Book.Isbn, c.IsIssued);

    private static readonly Expression<Func<LoanRow, LoanView>> LoanProjection =
        l => new LoanView(l.Id, l.ReaderId, l.IssuedOn, l.ReturnedOn, l.Status, l.LoanDays,
            new CopyView(l.Copy.Id, l.Copy.Book.Isbn, l.Copy.IsIssued));

    private IQueryable<BookRow> BookRows => _db.Books.AsNoTracking();
    private IQueryable<CopyRow> CopyRows => _db.Copies.AsNoTracking();
    private IQueryable<LoanRow> LoanRows => _db.Loans.AsNoTracking();

    // T = Book / BookCopy / Loan — одна таблиця; T = ILibraryEntity — усі три (так працює й InMemoryBookStore).
    private static bool Wants<T, TEntity>() => typeof(T).IsAssignableFrom(typeof(TEntity));

    private async Task<IReadOnlyList<T>> ListCore<T>(bool async, CancellationToken ct) where T : class, ILibraryEntity
    {
        var result = new List<T>();
        if (Wants<T, Book>())
            result.AddRange((await ToListCore(BookRows.OrderBy(b => b.Id), async, ct)).Select(RowMapping.ToDomain).OfType<T>());
        if (Wants<T, BookCopy>())
            result.AddRange((await ToListCore(CopyRows.OrderBy(c => c.Id).Select(CopyProjection), async, ct))
                .Select(RowMapping.ToDomain).OfType<T>());
        if (Wants<T, Loan>())
            result.AddRange((await ToListCore(LoanRows.OrderBy(l => l.Id).Select(LoanProjection), async, ct))
                .Select(RowMapping.ToDomain).OfType<T>());
        return result;
    }

    private async Task<T?> GetByIdCore<T>(string id, bool async, CancellationToken ct) where T : class, ILibraryEntity
    {
        if (string.IsNullOrWhiteSpace(id)) return null;
        id = id.Trim();

        if (Wants<T, Book>() && await SingleOrDefaultCore(BookRows.Where(b => b.Id == id), async, ct) is { } book)
            return RowMapping.ToDomain(book) as T;
        if (Wants<T, BookCopy>() &&
            await SingleOrDefaultCore(CopyRows.Where(c => c.Id == id).Select(CopyProjection), async, ct) is { } copy)
            return RowMapping.ToDomain(copy) as T;
        if (Wants<T, Loan>() &&
            await SingleOrDefaultCore(LoanRows.Where(l => l.Id == id).Select(LoanProjection), async, ct) is { } loan)
            return RowMapping.ToDomain(loan) as T;
        return null;
    }

    // ---------- Запис

    private async Task AddCore(ILibraryEntity item, bool async, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);

        // Контракт IBookStore: дубль id — InvalidOperationException. Id унікальні в межах усього сховища.
        if (await IdExists(item.Id, async, ct))
            throw new InvalidOperationException($"Запис з id={item.Id} уже існує.");

        switch (item)
        {
            case Book book:
                _db.Books.Add(RowMapping.ToRow(book));
                break;

            case BookCopy copy:
                string key = IsbnRules.Compact(copy.Isbn);
                string bookId = await SingleOrDefaultCore(BookRows.Where(b => b.IsbnKey == key).Select(b => b.Id), async, ct)
                                ?? throw new InvalidOperationException(
                                    $"Примірник {copy.Id}: у каталозі немає видання з ISBN {copy.Isbn}.");
                _db.Copies.Add(new CopyRow
                {
                    Id = copy.Id, BookId = bookId, IsIssued = copy.IsIssued, RegisteredAt = _clock.GetUtcNow()
                });
                break;

            case Loan loan:
                CopyRow copyRow = await FindTracked(_db.Copies, loan.Copy.Id, async, ct)
                                  ?? throw new InvalidOperationException(
                                      $"Видача {loan.Id}: примірника {loan.Copy.Id} немає у сховищі.");
                copyRow.IsIssued = loan.Copy.IsIssued; // Loan.Open уже видав примірник — зберігаємо обидва разом
                _db.Loans.Add(RowMapping.ToRow(loan));
                break;

            default:
                throw new ArgumentException($"Тип {item.GetType().Name} не зберігається в {nameof(EfBookStore)}.", nameof(item));
        }

        await SaveCore(async, ct);
    }

    private async Task UpdateCore(ILibraryEntity item, bool async, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(item);

        switch (item)
        {
            case Book book:
                RowMapping.CopyTo(book, await FindTracked(_db.Books, book.Id, async, ct) ?? throw Missing(book));
                break;

            case BookCopy copy:
                (await FindTracked(_db.Copies, copy.Id, async, ct) ?? throw Missing(copy)).IsIssued = copy.IsIssued;
                break;

            case Loan loan:
                LoanRow row = await FindTracked(_db.Loans, loan.Id, async, ct) ?? throw Missing(loan);
                row.Status = loan.Status;
                row.ReturnedOn = loan.ReturnedOn;
                // Close() повертає примірник на полицю: стан Loan і Copy мають зберегтися узгоджено.
                CopyRow copyRow = await FindTracked(_db.Copies, loan.Copy.Id, async, ct) ?? throw Missing(loan.Copy);
                copyRow.IsIssued = loan.Copy.IsIssued;
                break;

            default:
                throw new ArgumentException($"Тип {item.GetType().Name} не зберігається в {nameof(EfBookStore)}.", nameof(item));
        }

        await SaveCore(async, ct);
    }

    private async Task<bool> RemoveCore(string id, bool async, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        id = id.Trim();

        // Видання: примірники зникнуть каскадно (ON DELETE CASCADE). Примірник з видачами — ні (RESTRICT) → 409.
        if (await FindTracked(_db.Books, id, async, ct) is { } book) _db.Books.Remove(book);
        else if (await FindTracked(_db.Copies, id, async, ct) is { } copy) _db.Copies.Remove(copy);
        else if (await FindTracked(_db.Loans, id, async, ct) is { } loan) _db.Loans.Remove(loan);
        else return false;

        await SaveCore(async, ct);
        return true;
    }

    // ---------- Збереження і переклад помилок БД

    // SaveChanges: усі накопичені зміни — одним пакетом SQL у транзакції. Порушення обмежень SQLite
    // (UNIQUE, FOREIGN KEY) не доходять до клієнта як 500: стають DomainException → 409 в Api.
    private async Task SaveCore(bool async, CancellationToken ct)
    {
        try
        {
            if (async) await _db.SaveChangesAsync(ct);
            else _db.SaveChanges();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: 19 } sqlite) // SQLITE_CONSTRAINT
        {
            _db.ChangeTracker.Clear();
            throw Translate(ex, sqlite);
        }
    }

    private static Exception Translate(DbUpdateException ex, SqliteException sqlite)
    {
        string message = sqlite.Message;
        if (message.Contains("Books.IsbnKey", StringComparison.Ordinal))
        {
            string isbn = ex.Entries.Select(e => e.Entity).OfType<BookRow>().FirstOrDefault()?.Isbn ?? "?";
            return new DomainException($"Видання з ISBN {isbn} уже є в каталозі (обмеження UNIQUE у БД).", ex);
        }

        if (message.Contains("UNIQUE", StringComparison.Ordinal))
            return new InvalidOperationException("Запис з таким id уже існує.", ex);

        if (message.Contains("FOREIGN KEY", StringComparison.Ordinal))
            return new DomainException(
                "Запис пов'язаний з іншими даними (примірник має історію видач) — операцію відхилено.", ex);

        return new DomainException("Зміну відхилено обмеженням бази даних.", ex);
    }

    // ---------- Допоміжне: одна гілка для sync і async

    private async Task<bool> IdExists(string id, bool async, CancellationToken ct) =>
        await AnyCore(BookRows.Where(b => b.Id == id), async, ct)
        || await AnyCore(CopyRows.Where(c => c.Id == id), async, ct)
        || await AnyCore(LoanRows.Where(l => l.Id == id), async, ct);

    // Find спершу дивиться в трекер (без запиту до БД), потім — SELECT за первинним ключем.
    private static async Task<TRow?> FindTracked<TRow>(DbSet<TRow> set, string id, bool async, CancellationToken ct)
        where TRow : class =>
        async ? await set.FindAsync([id], ct) : set.Find(id);

    private static async Task<List<TItem>> ToListCore<TItem>(IQueryable<TItem> query, bool async, CancellationToken ct) =>
        async ? await query.ToListAsync(ct) : query.ToList();

    private static async Task<TItem?> SingleOrDefaultCore<TItem>(IQueryable<TItem> query, bool async, CancellationToken ct) =>
        async ? await query.SingleOrDefaultAsync(ct) : query.SingleOrDefault();

    private static async Task<bool> AnyCore<TItem>(IQueryable<TItem> query, bool async, CancellationToken ct) =>
        async ? await query.AnyAsync(ct) : query.Any();

    private static InvalidOperationException Missing(ILibraryEntity item) =>
        new($"Запису {item.GetType().Name} з id={item.Id} немає у сховищі — оновлювати нічого.");
}

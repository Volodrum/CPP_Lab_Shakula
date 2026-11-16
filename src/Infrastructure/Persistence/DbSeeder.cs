using Core;
using Core.Domain;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence;

// Дод. завдання 3: стартові дані (SampleData: 19 видань, 52 примірники, 21 видача) — лише в ПОРОЖНЮ базу.
// Повторний запуск нічого не дублює: є хоч одне видання — сідування пропускається. Усе — одним SaveChanges,
// тобто однією транзакцією: або повний каталог, або нічого.
// Чому не HasData у міграції: дані згенеровані кодом домену (Book.Create, Loan.Open) і залежать від поточної дати
// (рік видання ≤ поточний), а HasData вшив би їх у файл міграції назавжди.
public static class DbSeeder
{
    public static async Task<int> SeedAsync(AppDbContext db, IEnumerable<ILibraryEntity> items,
        TimeProvider? clock = null, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(items);
        if (await db.Books.AnyAsync(ct)) return 0;

        DateTimeOffset now = (clock ?? TimeProvider.System).GetUtcNow();
        List<ILibraryEntity> all = items.ToList();

        var books = all.OfType<Book>().Select(RowMapping.ToRow).ToList();
        var bookIdByIsbn = books.ToDictionary(b => b.IsbnKey, b => b.Id, StringComparer.Ordinal);

        var copies = all.OfType<BookCopy>().Select(c => new CopyRow
        {
            Id = c.Id,
            BookId = bookIdByIsbn.TryGetValue(IsbnRules.Compact(c.Isbn), out var bookId)
                ? bookId
                : throw new InvalidOperationException($"Примірник {c.Id}: немає видання з ISBN {c.Isbn}."),
            IsIssued = c.IsIssued,
            RegisteredAt = now
        }).ToList();

        var loans = all.OfType<Loan>().Select(RowMapping.ToRow).ToList();

        db.Books.AddRange(books);
        db.Copies.AddRange(copies);
        db.Loans.AddRange(loans);
        await db.SaveChangesAsync(ct);
        db.ChangeTracker.Clear();

        return books.Count + copies.Count + loans.Count;
    }

    public static Task<int> SeedSampleDataAsync(AppDbContext db, CancellationToken ct = default) =>
        SeedAsync(db, SampleData.Items(), ct: ct);
}

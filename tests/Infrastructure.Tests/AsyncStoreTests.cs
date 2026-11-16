using Core;
using Core.Domain;
using Core.Services;
using Infrastructure.Persistence;

namespace Infrastructure.Tests;

// Дод. завдання 1: асинхронний контракт IBookStoreAsync, транзакції і скасування; CatalogService над SQLite.
public class AsyncStoreTests
{
    [Fact]
    public async Task AsyncMethods_RoundTrip()
    {
        using var test = new TestDatabase();
        await using (var db = test.NewContext())
        {
            var store = test.NewStore(db);
            await store.AddAsync(Book.Create("B-1", "978-966-01-0001-2", "Кобзар", 1840));
            await store.AddAsync(BookCopy.Register("C-1", "978-966-01-0001-2"));
            await store.UpdateAsync(Book.Create("B-1", "978-966-01-0001-2", "Кобзар (1840)", 1840));
        }

        await using (var db = test.NewContext())
        {
            var store = test.NewStore(db);
            Assert.Equal("Кобзар (1840)", (await store.GetByIdAsync<Book>("B-1"))!.Title);
            Assert.Single(await store.ListAsync<BookCopy>());
            Assert.True(await store.RemoveAsync("C-1"));
            Assert.False(await store.RemoveAsync("C-1"));
        }
    }

    [Fact] // InTransactionAsync: виняток посередині — відкат УСІХ змін, навіть уже збережених SaveChanges
    public async Task Transaction_RollsBack_WhenWorkFails()
    {
        using var test = new TestDatabase();
        await using (var db = test.NewContext())
        {
            var store = test.NewStore(db);
            await Assert.ThrowsAsync<DomainException>(() => store.InTransactionAsync<int>(async ct =>
            {
                await store.AddAsync(Book.Create("B-1", "978-966-01-0001-2", "Кобзар", 1840), ct);
                await store.AddAsync(BookCopy.Register("C-1", "978-966-01-0001-2"), ct);
                throw new DomainException("щось пішло не так після двох SaveChanges");
            }));
            Assert.Empty(await store.ListAsync<ILibraryEntity>()); // той самий контекст після відкату
        }

        await using (var db = test.NewContext())
            Assert.Empty(await test.NewStore(db).ListAsync<ILibraryEntity>());
    }

    [Fact]
    public async Task CancelledToken_StopsQuery()
    {
        using var test = new TestDatabase();
        await using var db = test.NewContext();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => test.NewStore(db).ListAsync<Book>(cts.Token));
    }

    [Fact] // CatalogService (той самий, що в Api) над EfBookStore: створення, заміна, видалення з каскадом
    public async Task CatalogService_OverEfStore_FullCycle()
    {
        using var test = new TestDatabase();
        await using var db = test.NewContext();
        var catalog = new CatalogService((Core.Abstractions.IBookStoreAsync)test.NewStore(db));

        CatalogEntry created = await catalog.CreateAsync(new BookDraft("978-966-01-0001-2", "Кобзар", 1840, null, 3));
        CatalogEntry? replaced = await catalog.ReplaceAsync(created.Book.Id, new BookDraft("978-966-01-0001-2", "Кобзар", 1840, "Т. Шевченко", 1));
        Assert.Equal((3, 1, "Т. Шевченко"), (created.Copies, replaced!.Copies, replaced.Book.Author));

        Assert.True(await catalog.DeleteAsync(created.Book.Id));
        Assert.Empty(db.Copies);
    }

    [Fact] // два «процеси» (два контексти, кожен зі своїм CatalogWriteGate) — унікальний індекс БД не пустить дубль
    public async Task TwoServicesWithoutSharedGate_DuplicateIsbn_SecondGetsDomainException()
    {
        using var test = new TestDatabase();
        await using var db1 = test.NewContext();
        await using var db2 = test.NewContext();
        var first = test.NewStore(db1);
        var second = test.NewStore(db2);

        await first.AddAsync(Book.Create("B-1", "978-966-01-0001-2", "Кобзар", 1840));
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            second.AddAsync(Book.Create("B-2", "9789660100012", "Кобзар (копія)", 1840)));
        Assert.Contains("978", ex.Message);
    }

    [Fact] // рядок підключення: відносний шлях → абсолютний від ContentRoot, каталог створюється; :memory: — без змін
    public void ConnectionString_RelativePath_ResolvedFromContentRoot()
    {
        string root = Path.Combine(Path.GetTempPath(), "crossapp-tests", $"root-{Guid.NewGuid():N}");
        try
        {
            string resolved = SqliteConnectionStrings.Resolve("Data Source=data/app.db", root);
            string expected = Path.Combine(root, "data", "app.db");
            Assert.Equal($"Data Source={expected}", resolved);
            Assert.True(Directory.Exists(Path.Combine(root, "data")));

            Assert.Equal("Data Source=:memory:", SqliteConnectionStrings.Resolve("Data Source=:memory:", root));
            Assert.Contains(Path.Combine(root, "data", "app.db"), SqliteConnectionStrings.Resolve(null, root));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}

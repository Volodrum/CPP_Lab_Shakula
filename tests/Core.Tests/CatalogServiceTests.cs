using Core.Domain;
using Core.Services;
using Core.Storage;

namespace Core.Tests;

// Тиждень 10: CRUD каталогу, яким користується Api. Без HTTP — лише Core і сховище в пам'яті.
public class CatalogServiceTests
{
    private const string Isbn = "978-617-12-3456-7";
    private static readonly DateOnly Day = new(2026, 10, 1);

    private static BookDraft Draft(int copies = 2, string isbn = Isbn, string title = "Кобзар") =>
        new(isbn, title, 1840, "Тарас Шевченко", copies);

    [Fact]
    public async Task CreateAsync_AddsBookWithRequestedCopies()
    {
        var store = new InMemoryBookStore();
        var catalog = new CatalogService(store);

        CatalogEntry created = await catalog.CreateAsync(Draft(copies: 3));

        Assert.StartsWith("B-", created.Book.Id);
        Assert.Equal((3, 3), (created.Copies, created.OnShelf));
        Assert.Equal(3, store.List<BookCopy>().Count(c => c.Isbn == created.Book.Isbn));
        Assert.Equal(created, await catalog.GetAsync(created.Book.Id));
    }

    [Fact] // унікальність ISBN — правило на весь каталог; дефіси не роблять ISBN іншим
    public async Task CreateAsync_DuplicateIsbn_ThrowsDomainException_AndAddsNothing()
    {
        var store = new InMemoryBookStore();
        var catalog = new CatalogService(store);
        await catalog.CreateAsync(Draft());

        await Assert.ThrowsAsync<DomainException>(() => catalog.CreateAsync(Draft(isbn: "9786171234567")));

        Assert.Single(store.List<Book>());
        Assert.Equal(2, store.List<BookCopy>().Count);
    }

    [Fact] // SemaphoreSlim: паралельні POST з одним ISBN — рівно один успіх
    public async Task CreateAsync_ConcurrentSameIsbn_OnlyOneSucceeds()
    {
        var store = new SynchronizedBookStore(new InMemoryBookStore());
        var catalog = new CatalogService(store);

        var attempts = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            try { await catalog.CreateAsync(Draft(copies: 1)); return true; }
            catch (DomainException) { return false; }
        }));
        bool[] results = await Task.WhenAll(attempts);

        Assert.Equal(1, results.Count(ok => ok));
        Assert.Single(store.List<Book>());
        Assert.Single(store.List<BookCopy>());
    }

    [Fact]
    public async Task ReplaceAsync_ChangesFieldsAndCopyCount_IsIdempotent()
    {
        var store = new InMemoryBookStore();
        var catalog = new CatalogService(store);
        CatalogEntry created = await catalog.CreateAsync(Draft(copies: 3));
        var replacement = Draft(copies: 1, title: "Кобзар (1840)");

        CatalogEntry? first = await catalog.ReplaceAsync(created.Book.Id, replacement);
        CatalogEntry? second = await catalog.ReplaceAsync(created.Book.Id, replacement);

        Assert.NotNull(first);
        Assert.Equal("Кобзар (1840)", first.Book.Title);
        Assert.Equal(created.Book.Id, first.Book.Id);
        Assert.Equal(1, first.Copies);
        // Book — клас, тож порівнюємо значення полів, а не посилання: другий PUT дав той самий стан.
        Assert.Equal(first.Book.ToString(), second!.Book.ToString());
        Assert.Equal((first.Copies, first.OnShelf), (second.Copies, second.OnShelf));
        Assert.Single(store.List<BookCopy>());
    }

    [Fact]
    public async Task ReplaceAsync_MissingId_ReturnsNull()
    {
        var catalog = new CatalogService(new InMemoryBookStore());

        Assert.Null(await catalog.ReplaceAsync("B-404", Draft()));
    }

    [Fact] // виданий примірник не зникає мовчки: 409 і жодної півзміни
    public async Task ReplaceAsync_RemovingIssuedCopy_ThrowsDomainException_AndKeepsState()
    {
        var store = new InMemoryBookStore();
        var catalog = new CatalogService(store);
        var lending = new LendingService(store);
        CatalogEntry created = await catalog.CreateAsync(Draft(copies: 1));
        lending.IssueCopy(store.List<BookCopy>().Single().Id, "R-1", Day);

        await Assert.ThrowsAsync<DomainException>(
            () => catalog.ReplaceAsync(created.Book.Id, Draft(copies: 0, title: "Нова назва")));

        Assert.Equal("Кобзар", store.GetById<Book>(created.Book.Id)!.Title);
        Assert.Single(store.List<BookCopy>());
    }

    [Fact] // примірники позначено старим ISBN — змінити його можна лише виданню без примірників
    public async Task ReplaceAsync_IsbnChangeWithCopies_ThrowsDomainException()
    {
        var catalog = new CatalogService(new InMemoryBookStore());
        CatalogEntry created = await catalog.CreateAsync(Draft(copies: 1));

        await Assert.ThrowsAsync<DomainException>(
            () => catalog.ReplaceAsync(created.Book.Id, Draft(copies: 1, isbn: "978-0-13-468599-1")));

        CatalogEntry? withoutCopies = await catalog.ReplaceAsync(created.Book.Id, Draft(copies: 0));
        CatalogEntry? renamed = await catalog.ReplaceAsync(created.Book.Id, Draft(copies: 0, isbn: "978-0-13-468599-1"));
        Assert.Equal(0, withoutCopies!.Copies);
        Assert.Equal("978-0-13-468599-1", renamed!.Book.Isbn);
    }

    [Fact]
    public async Task DeleteAsync_RemovesBookWithFreeCopies_SecondCallReturnsFalse()
    {
        var store = new InMemoryBookStore();
        var catalog = new CatalogService(store);
        CatalogEntry created = await catalog.CreateAsync(Draft(copies: 2));

        Assert.True(await catalog.DeleteAsync(created.Book.Id));
        Assert.False(await catalog.DeleteAsync(created.Book.Id));
        Assert.Null(await catalog.GetAsync(created.Book.Id));
        Assert.Empty(store.List<BookCopy>());
    }

    [Fact] // закрита видача теж тримає примірник: інакше файл сховища мав би видачу без примірника
    public async Task DeleteAsync_CopyWithLoanHistory_ThrowsDomainException()
    {
        var store = new InMemoryBookStore();
        var catalog = new CatalogService(store);
        var lending = new LendingService(store);
        CatalogEntry created = await catalog.CreateAsync(Draft(copies: 1));
        Loan loan = lending.IssueCopy(store.List<BookCopy>().Single().Id, "R-1", Day);
        lending.ReturnCopy(loan.Id, Day.AddDays(3));

        await Assert.ThrowsAsync<DomainException>(() => catalog.DeleteAsync(created.Book.Id));
        Assert.NotNull(store.GetById<Book>(created.Book.Id));
    }

    [Fact] // фільтр автора без регістру + пагінація з total усіх збігів
    public async Task SearchAsync_FiltersByAuthor_AndPages()
    {
        var catalog = new CatalogService(new InMemoryBookStore(SampleData.Items()));

        CatalogPage first = await catalog.SearchAsync(new CatalogQuery(Author: "мартін", PageSize: 2));
        CatalogPage second = await catalog.SearchAsync(new CatalogQuery(Author: "мартін", Page: 2, PageSize: 2));
        CatalogPage beyond = await catalog.SearchAsync(new CatalogQuery(Author: "мартін", Page: int.MaxValue, PageSize: 2));

        Assert.Equal(3, first.Total);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(second.Items);
        Assert.Empty(beyond.Items);
        Assert.Empty(first.Items.Select(e => e.Book.Id).Intersect(second.Items.Select(e => e.Book.Id)));
    }

    [Fact] // search шукає і в назві, і в ISBN (без урахування дефісів)
    public async Task SearchAsync_MatchesTitleOrCompactIsbn()
    {
        var catalog = new CatalogService(new InMemoryBookStore(SampleData.Items()));

        CatalogPage byTitle = await catalog.SearchAsync(new CatalogQuery(Search: "design"));
        CatalogPage byIsbn = await catalog.SearchAsync(new CatalogQuery(Search: "9780201633610"));

        Assert.Equal(4, byTitle.Total);
        Assert.Equal("B-002", Assert.Single(byIsbn.Items).Book.Id);
    }

    [Fact]
    public async Task SearchAsync_CancelledToken_Throws()
    {
        var catalog = new CatalogService(new InMemoryBookStore());
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => catalog.SearchAsync(new CatalogQuery(), cts.Token));
    }
}

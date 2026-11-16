using Core;
using Core.Domain;
using Core.Services;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests;

// EfBookStore за контрактом IBookStore: ті самі операції, що й InMemoryBookStore, але дані — у файлі SQLite.
// Кожна перевірка «після перезапуску» читає новим DbContext, тож нічого не береться з кешу трекера.
public class EfBookStoreTests
{
    private static Book Kobzar(string id = "B-100", string isbn = "978-966-01-0001-2") =>
        Book.Create(id, isbn, "Кобзар", 1840, "Тарас Шевченко");

    [Fact] // самоперевірка 4: дані живуть між «запусками», Book відновлюється через Book.Create
    public void AddedBookAndCopies_AreReadBackByNewContext()
    {
        using var test = new TestDatabase();
        using (var db = test.NewContext())
        {
            var store = test.NewStore(db);
            store.Add(Kobzar());
            store.Add(BookCopy.Register("C-100", "978-966-01-0001-2"));
            store.Add(BookCopy.Register("C-101", "978-966-01-0001-2"));
        }

        using (var db = test.NewContext())
        {
            var store = test.NewStore(db);
            Book book = Assert.Single(store.List<Book>());
            Assert.Equal(Kobzar().ToString(), book.ToString());
            Assert.Equal(["C-100", "C-101"], store.List<BookCopy>().Select(c => c.Id));
            Assert.All(store.List<BookCopy>(), c => Assert.True(book.HasIsbn(c.Isbn)));
            Assert.Equal(3, store.List<ILibraryEntity>().Count);
        }
    }

    [Fact] // інваріанти домену перевіряються і при читанні: «зіпсований» рядок не стає Book
    public void CorruptedRow_FailsInBookCreate_NotSilently()
    {
        using var test = new TestDatabase();
        using (var db = test.NewContext())
        {
            db.Books.Add(new BookRow { Id = "B-BAD", Isbn = "978-966-01-0001-2", IsbnKey = "9789660100012", Title = " ", Year = 1840 });
            db.SaveChanges();
        }

        using var read = test.NewContext();
        Assert.Throws<ArgumentException>(() => test.NewStore(read).GetById<Book>("B-BAD"));
    }

    [Fact]
    public void GetById_IsCaseInsensitive_AndTypeAware()
    {
        using var test = new TestDatabase();
        using var db = test.NewContext();
        var store = test.NewStore(db);
        store.Add(Kobzar());

        Assert.NotNull(store.GetById<Book>("b-100"));
        Assert.NotNull(store.GetById<ILibraryEntity>("B-100"));
        Assert.Null(store.GetById<BookCopy>("B-100"));
        Assert.Null(store.GetById<Book>("B-404"));
    }

    [Fact] // контракт IBookStore: дубль id — InvalidOperationException
    public void Add_DuplicateId_Throws()
    {
        using var test = new TestDatabase();
        using var db = test.NewContext();
        var store = test.NewStore(db);
        store.Add(Kobzar());

        Assert.Throws<InvalidOperationException>(() => store.Add(Kobzar(isbn: "978-966-01-0002-9")));
    }

    [Fact] // унікальний індекс IsbnKey: той самий ISBN з іншими дефісами — DomainException (у Api це 409), не 500
    public void Add_SameIsbnDifferentHyphens_DbUniqueBecomesDomainException()
    {
        using var test = new TestDatabase();
        using var db = test.NewContext();
        var store = test.NewStore(db);
        store.Add(Kobzar());

        var ex = Assert.Throws<DomainException>(() => store.Add(Kobzar("B-101", "9789660100012")));
        Assert.Contains("UNIQUE", ex.Message);
        Assert.Single(store.List<Book>()); // контекст очищено — у БД так і лишилось одне видання
    }

    [Fact]
    public void Add_CopyOfUnknownIsbn_Throws()
    {
        using var test = new TestDatabase();
        using var db = test.NewContext();

        Assert.Throws<InvalidOperationException>(() => test.NewStore(db).Add(BookCopy.Register("C-1", "978-966-01-0001-2")));
    }

    [Fact] // DeleteBehavior.Cascade: видалили видання — БД сама видалила його примірники
    public void RemoveBook_CascadesToCopies()
    {
        using var test = new TestDatabase();
        using (var db = test.NewContext())
        {
            var store = test.NewStore(db);
            store.Add(Kobzar());
            store.Add(BookCopy.Register("C-100", "978-966-01-0001-2"));
        }

        using (var db = test.NewContext())
            Assert.True(test.NewStore(db).Remove("B-100"));

        using (var db = test.NewContext())
        {
            Assert.Empty(db.Copies);
            Assert.False(test.NewStore(db).Remove("B-100"));
        }
    }

    [Fact] // DeleteBehavior.Restrict: примірник з історією видач не видаляється навіть в обхід CatalogService
    public async Task RemoveCopyWithLoan_IsRejectedByForeignKey()
    {
        using var test = new TestDatabase();
        using var db = test.NewContext();
        var store = test.NewStore(db);
        await DbSeeder.SeedSampleDataAsync(db);
        string copyId = store.List<Loan>()[0].Copy.Id;

        Assert.Throws<DomainException>(() => store.Remove(copyId));
        Assert.NotNull(store.GetById<BookCopy>(copyId));
    }

    [Fact] // той самий LendingService тижнів 5–8 працює над EF-сховищем без жодної зміни
    public void LendingService_OverEfStore_PersistsIssueAndReturn()
    {
        using var test = new TestDatabase();
        string loanId;
        using (var db = test.NewContext())
        {
            var lending = new LendingService(test.NewStore(db));
            lending.AddBook("978-966-01-0001-2", "Кобзар", 1840, "Тарас Шевченко");
            BookCopy copy = lending.RegisterCopy("978-966-01-0001-2");
            loanId = lending.IssueCopy(copy.Id, "R-01", new DateOnly(2026, 11, 1)).Id;
        }

        using (var db = test.NewContext())
        {
            var lending = new LendingService(test.NewStore(db));
            Loan loan = lending.Find<Loan>(loanId)!;
            Assert.Equal((LoanStatus.Open, true), (loan.Status, loan.Copy.IsIssued));
            lending.ReturnCopy(loanId, new DateOnly(2026, 11, 10));
        }

        using (var db = test.NewContext())
        {
            Loan loan = test.NewStore(db).GetById<Loan>(loanId)!;
            Assert.Equal((LoanStatus.Closed, false, new DateOnly(2026, 11, 10)), (loan.Status, loan.Copy.IsIssued, loan.ReturnedOn));
            Assert.Equal("Closed", db.Database.SqlQueryRaw<string>("SELECT Status AS Value FROM Loans").Single()); // enum — текстом
        }
    }

    [Fact] // дод. завдання 3: сідування лише в порожню БД; повтор нічого не дублює
    public async Task Seed_FillsEmptyDatabaseOnce()
    {
        using var test = new TestDatabase();
        await using var db = test.NewContext();

        Assert.Equal(19 + 52 + 21, await DbSeeder.SeedSampleDataAsync(db));
        Assert.Equal(0, await DbSeeder.SeedSampleDataAsync(db));

        var store = test.NewStore(db);
        Assert.Equal((19, 52, 21), (store.List<Book>().Count, store.List<BookCopy>().Count, store.List<Loan>().Count));
        // Видачі відновлюються через Loan.FromDto: стан примірника узгоджений зі статусом видачі
        Assert.All(store.List<Loan>().Where(l => l.IsActive), l => Assert.True(l.Copy.IsIssued));
    }
}

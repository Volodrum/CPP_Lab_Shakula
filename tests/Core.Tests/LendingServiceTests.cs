using Core.Domain;
using Core.Events;
using Core.Services;
using Core.Storage;

namespace Core.Tests;

// Інваріанти й подія сервісу. Сховище — InMemoryBookStore, нове в кожному тесті: ні диска, ні спільного стану.
public class LendingServiceTests
{
    private static readonly DateOnly Day = new(2026, 10, 1);

    [Fact] // варіант «Бібліотека»: «примірник уже видано» — доменна відмова, стан не змінився
    public void IssueCopy_WhenCopyAlreadyIssued_ThrowsDomainException_AndKeepsState()
    {
        var store = new InMemoryBookStore();
        var service = new LendingService(store);
        service.AddBook(TestData.BigIsbn, "Мова програмування C", 1988);
        BookCopy copy = service.RegisterCopy(TestData.BigIsbn);
        Loan first = service.IssueCopy(copy.Id, "R-1", Day);

        var ex = Assert.Throws<DomainException>(() => service.IssueCopy(copy.Id, "R-2", Day));

        Assert.Contains("вже виданий", ex.Message);
        Assert.Equal(first.Id, Assert.Single(store.List<Loan>()).Id);
        Assert.Equal(0, service.CountActiveLoans("R-2"));
    }

    [Fact] // варіант «Бібліотека»: повернення невиданого примірника (видачу вже закрито)
    public void ReturnCopy_WhenLoanAlreadyClosed_ThrowsDomainException_AndKeepsState()
    {
        var store = new InMemoryBookStore();
        var service = new LendingService(store);
        service.AddBook(TestData.BigIsbn, "Мова програмування C", 1988);
        BookCopy copy = service.RegisterCopy(TestData.BigIsbn);
        Loan loan = service.IssueCopy(copy.Id, "R-1", Day);
        service.ReturnCopy(loan.Id, Day.AddDays(3));

        Assert.Throws<DomainException>(() => service.ReturnCopy(loan.Id, Day.AddDays(10)));

        Loan stored = store.GetById<Loan>(loan.Id)!;
        Assert.Equal(Day.AddDays(3), stored.ReturnedOn); // дата першого повернення не переписана
        Assert.False(store.GetById<BookCopy>(copy.Id)!.IsIssued);
    }

    [Fact] // той самий інваріант на рівні сутності: примірник на полиці «повернути» не можна
    public void Return_OfCopyOnShelf_ThrowsDomainException()
    {
        BookCopy copy = BookCopy.Register("C-1", TestData.BigIsbn);

        Assert.Throws<DomainException>(copy.Return);
        Assert.False(copy.IsIssued);
    }

    [Theory] // поганий аргумент — ArgumentException, а не DomainException; у сховище нічого не потрапило
    [InlineData(1449)]
    [InlineData(3000)]
    public void AddBook_WithYearOutOfRange_ThrowsArgumentOutOfRange_AndAddsNothing(int year)
    {
        var store = new InMemoryBookStore();
        var service = new LendingService(store);

        Assert.Throws<ArgumentOutOfRangeException>(() => service.AddBook(TestData.BigIsbn, "Книга", year));
        Assert.Empty(store.List<Book>());
    }

    [Theory] // унікальність ISBN — правило каталогу (кілька сутностей), тому DomainException
    [InlineData(TestData.BigIsbn)]
    [InlineData("9780000000010")] // той самий ISBN без дефісів
    public void AddBook_WithIsbnAlreadyInCatalog_ThrowsDomainException(string sameIsbn)
    {
        var store = new InMemoryBookStore();
        var service = new LendingService(store);
        service.AddBook(TestData.BigIsbn, "Перше видання", 2001);

        Assert.Throws<DomainException>(() => service.AddBook(sameIsbn, "Дубль", 2002));
        Assert.Single(store.List<Book>());
    }

    [Fact]
    public void IssueCopy_OverReaderLimit_ThrowsDomainException()
    {
        var service = new LendingService(new InMemoryBookStore());
        service.AddBook(TestData.BigIsbn, "Популярна книга", 2020);
        for (int i = 0; i < LendingService.MaxActiveLoansPerReader; i++)
            service.IssueCopy(service.RegisterCopy(TestData.BigIsbn).Id, "R-1", Day);
        BookCopy extra = service.RegisterCopy(TestData.BigIsbn);

        Assert.Throws<DomainException>(() => service.IssueCopy(extra.Id, "R-1", Day));
        Assert.Equal(LendingService.MaxActiveLoansPerReader, service.CountActiveLoans("R-1"));
    }

    [Fact] // подія з тижня 6 як спосіб перевірки: ні консолі, ні диска — лише список у пам'яті
    public void AddBook_RaisesChangedExactlyOnce()
    {
        var service = new LendingService(new InMemoryBookStore());
        var events = new List<LibraryChangedEventArgs>();
        service.Changed += (_, e) => events.Add(e);

        Book book = service.AddBook(TestData.BigIsbn, "Гвинти й гайки", 2010);

        LibraryChangedEventArgs e = Assert.Single(events);
        Assert.Equal(LibraryChangeKind.BookAdded, e.Kind);
        Assert.Equal(book.Id, e.EntityId);
    }

    [Fact] // подія піднімається лише після успішної зміни
    public void IssueCopy_WhenRejected_RaisesNoEvent()
    {
        var service = new LendingService(new InMemoryBookStore());
        service.AddBook(TestData.BigIsbn, "Книга", 2010);
        BookCopy copy = service.RegisterCopy(TestData.BigIsbn);
        service.IssueCopy(copy.Id, "R-1", Day);
        var events = new List<LibraryChangedEventArgs>();
        service.Changed += (_, e) => events.Add(e);

        Assert.Throws<DomainException>(() => service.IssueCopy(copy.Id, "R-2", Day));
        Assert.Empty(events);
    }
}

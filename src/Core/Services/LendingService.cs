using Core.Abstractions;
using Core.Domain;

namespace Core.Services;

// Бізнес-операції бібліотеки. Сервіс не знає слів «File» і «Dictionary»: він отримує IBookStore
// через конструктор, а яку саме реалізацію — вирішує composition root у Cli.
// Інваріанти однієї сутності перевіряють Book/BookCopy/Loan; тут — лише правила на кілька сутностей
// (ліміт видач на читача, унікальний ISBN у каталозі, не вилучати видання з примірниками).
public sealed class LendingService(IBookStore store)
{
    public const int MaxActiveLoansPerReader = 5;

    private readonly IBookStore _store = store ?? throw new ArgumentNullException(nameof(store));

    public Book AddBook(string isbn, string title, int year, string? author = null)
    {
        Book book = Book.Create(NewId("B"), isbn, title, year, author); // інваріанти вже перевірив Book.Create

        Book? existing = FindByIsbn(book.Isbn);
        if (existing is not null)
            throw new InvalidOperationException($"Видання з ISBN {book.Isbn} уже є в каталозі: {existing.Id}.");

        _store.Add(book);
        return book;
    }

    // Новий примірник наявного видання надходить на полицю.
    public BookCopy RegisterCopy(string isbn)
    {
        Book book = FindByIsbn(isbn)
            ?? throw new InvalidOperationException($"Немає видання з ISBN {isbn}: спершу додайте книгу в каталог.");

        BookCopy copy = BookCopy.Register(NewId("C"), book.Isbn);
        _store.Add(copy);
        return copy;
    }

    public Loan IssueCopy(string copyId, string readerId, DateOnly issuedOn)
    {
        BookCopy copy = _store.GetById<BookCopy>(copyId)
            ?? throw new InvalidOperationException($"Немає примірника з id={copyId}.");

        if (string.IsNullOrWhiteSpace(readerId))
            throw new ArgumentException("Ідентифікатор читача обов'язковий", nameof(readerId));

        int active = CountActiveLoans(readerId);
        if (active >= MaxActiveLoansPerReader)
            throw new InvalidOperationException(
                $"Читач {readerId.Trim()} уже має {active} незакритих видач (ліміт {MaxActiveLoansPerReader}), " +
                $"видати примірник {copy.Id} неможливо");

        Loan loan = Loan.Open(NewId("L"), copy, readerId, issuedOn); // copy.Issue() — метод сутності з тижня 4
        _store.Add(loan);
        _store.Update(copy);
        return loan;
    }

    public Loan ReturnCopy(string loanId, DateOnly returnedOn)
    {
        Loan loan = GetLoan(loanId);
        loan.Close(returnedOn); // повертає й примірник: Copy.Return()
        _store.Update(loan);
        _store.Update(loan.Copy);
        return loan;
    }

    public Loan ReportLost(string loanId)
    {
        Loan loan = GetLoan(loanId);
        loan.MarkLost();
        _store.Update(loan);
        return loan;
    }

    // Вилучити видання з каталогу можна лише тоді, коли в нього не лишилось примірників.
    public bool RemoveBook(string bookId)
    {
        Book? book = _store.GetById<Book>(bookId);
        if (book is null) return false;

        int copies = CopiesOf(book).Count;
        if (copies > 0)
            throw new InvalidOperationException(
                $"Видання {book.Id} має {copies} примірн., вилучити його з каталогу неможливо.");

        return _store.Remove(book.Id);
    }

    public int CountActiveLoans(string readerId) =>
        _store.List<Loan>().Count(l => l.IsActive && string.Equals(l.ReaderId, readerId.Trim(), StringComparison.Ordinal));

    public IReadOnlyList<T> All<T>() where T : class, ILibraryEntity => _store.List<T>();

    public T? Find<T>(string id) where T : class, ILibraryEntity => _store.GetById<T>(id);

    // Додаткове завдання 2: умову пошуку передає викликач як Func<T, bool> (підготовка до тижнів 6–7).
    public IReadOnlyList<T> Search<T>(Func<T, bool> predicate) where T : class, ILibraryEntity
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _store.List<T>().Where(predicate).ToList();
    }

    public Book? FindByIsbn(string isbn) => Search<Book>(b => b.HasIsbn(isbn)).FirstOrDefault();

    public IReadOnlyList<BookCopy> CopiesOf(Book book) => Search<BookCopy>(c => book.HasIsbn(c.Isbn));

    private Loan GetLoan(string loanId) =>
        _store.GetById<Loan>(loanId) ?? throw new InvalidOperationException($"Немає видачі з id={loanId}.");

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}

using Core.Abstractions;
using Core.Domain;
using Core.Events;

namespace Core.Services;

// Бізнес-операції бібліотеки. Сервіс не знає слів «File» і «Dictionary»: він отримує IBookStore
// через конструктор, а яку саме реалізацію — вирішує composition root у Cli.
// Інваріанти однієї сутності перевіряють Book/BookCopy/Loan; тут — лише правила на кілька сутностей
// (ліміт видач на читача, унікальний ISBN у каталозі, не вилучати видання з примірниками).
// З тижня 6 сервіс повідомляє про кожну успішну зміну подією Changed; що робити з повідомленням — вирішує підписник.
public sealed class LendingService(IBookStore store, INotificationSink? sink = null)
{
    public const int MaxActiveLoansPerReader = 5;

    private readonly IBookStore _store = store ?? throw new ArgumentNullException(nameof(store));
    private readonly INotificationSink? _sink = sink; // додаткове завдання 1; без нього працює лише подія

    public event EventHandler<LibraryChangedEventArgs>? Changed;

    public Book AddBook(string isbn, string title, int year, string? author = null)
    {
        Book book = Book.Create(NewId("B"), isbn, title, year, author); // інваріанти вже перевірив Book.Create

        Book? existing = FindByIsbn(book.Isbn);
        if (existing is not null)
            throw new InvalidOperationException($"Видання з ISBN {book.Isbn} уже є в каталозі: {existing.Id}.");

        _store.Add(book);
        OnChanged(LibraryChangeKind.BookAdded, book.Id, book.Isbn, shelfDelta: 0); // після зміни, не до
        return book;
    }

    // Новий примірник наявного видання надходить на полицю.
    public BookCopy RegisterCopy(string isbn)
    {
        Book book = FindByIsbn(isbn)
            ?? throw new InvalidOperationException($"Немає видання з ISBN {isbn}: спершу додайте книгу в каталог.");

        BookCopy copy = BookCopy.Register(NewId("C"), book.Isbn);
        _store.Add(copy);
        OnChanged(LibraryChangeKind.CopyRegistered, copy.Id, copy.Isbn, shelfDelta: +1, copyId: copy.Id);
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

        Loan loan = Loan.Open(NewId("L"), copy, readerId, issuedOn); // якщо copy.Issue() кине виняток — події не буде
        _store.Add(loan);
        _store.Update(copy);
        OnChanged(LibraryChangeKind.CopyIssued, loan.Id, copy.Isbn, shelfDelta: -1, loan: loan);
        return loan;
    }

    public Loan ReturnCopy(string loanId, DateOnly returnedOn)
    {
        Loan loan = GetLoan(loanId);
        loan.Close(returnedOn); // повертає й примірник: Copy.Return()
        _store.Update(loan);
        _store.Update(loan.Copy);
        OnChanged(LibraryChangeKind.CopyReturned, loan.Id, loan.Copy.Isbn, shelfDelta: +1, loan: loan);
        return loan;
    }

    public Loan ReportLost(string loanId)
    {
        Loan loan = GetLoan(loanId);
        loan.MarkLost();
        _store.Update(loan);
        OnChanged(LibraryChangeKind.CopyLost, loan.Id, loan.Copy.Isbn, shelfDelta: 0, loan: loan); // примірник і так не на полиці
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

        bool removed = _store.Remove(book.Id);
        if (removed)
            OnChanged(LibraryChangeKind.BookRemoved, book.Id, book.Isbn, shelfDelta: 0);
        return removed;
    }

    public int CountActiveLoans(string readerId) =>
        _store.List<Loan>().Count(l => l.IsActive && string.Equals(l.ReaderId, readerId.Trim(), StringComparison.Ordinal));

    public IReadOnlyList<T> All<T>() where T : class, ILibraryEntity => _store.List<T>();

    public T? Find<T>(string id) where T : class, ILibraryEntity => _store.GetById<T>(id);

    // Критерій пошуку задає той, хто викликає: Func<T, bool> повертає відповідь, тому тут делегат, а не подія.
    public IReadOnlyList<T> Search<T>(Func<T, bool> predicate) where T : class, ILibraryEntity
    {
        ArgumentNullException.ThrowIfNull(predicate);
        return _store.List<T>().Where(predicate).ToList();
    }

    public Book? FindByIsbn(string isbn) => Search<Book>(b => b.HasIsbn(isbn)).FirstOrDefault();

    public IReadOnlyList<BookCopy> CopiesOf(Book book) => Search<BookCopy>(c => book.HasIsbn(c.Isbn));

    // Єдине місце, де піднімається подія. Викликається лише після того, як стан змінено і збережено:
    // якщо інваріант кинув виняток вище, до цього рядка виконання не дійде.
    private void OnChanged(LibraryChangeKind kind, string entityId, string isbn, int shelfDelta,
        string? copyId = null, Loan? loan = null)
    {
        if (Changed is null && _sink is null) return; // нікому повідомляти — не рахуємо полицю даремно

        var change = new LibraryChangedEventArgs(kind, entityId, isbn, shelfDelta, CountOnShelf(isbn),
            copyId ?? loan?.Copy.Id, loan?.ReaderId, loan?.DueOn);

        Changed?.Invoke(this, change);
        _sink?.Notify(change);
    }

    private int CountOnShelf(string isbn) =>
        Search<BookCopy>(c => !c.IsIssued && IsbnRules.Compact(c.Isbn) == IsbnRules.Compact(isbn)).Count;

    private Loan GetLoan(string loanId) =>
        _store.GetById<Loan>(loanId) ?? throw new InvalidOperationException($"Немає видачі з id={loanId}.");

    private static string NewId(string prefix) => $"{prefix}-{Guid.NewGuid().ToString("N")[..8]}";
}

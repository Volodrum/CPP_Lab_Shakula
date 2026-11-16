using Core.Domain;
using Core.Dto;

namespace Infrastructure.Persistence;

// Перетворення Row ↔ доменний об'єкт в одному місці. Назад у домен — лише через фабрики з перевірками
// (Book.Create, BookCopy.FromDto, Loan.FromDto): пошкоджений рядок БД дасть виняток, а не «тихий» невалідний об'єкт.
// Приватні конструктори й рефлексія тут не використовуються.
internal static class RowMapping
{
    // ---------- Book
    public static Book ToDomain(BookRow row) => Book.Create(row.Id, row.Isbn, row.Title, row.Year, row.Author);

    public static BookRow ToRow(Book book) => new()
    {
        Id = book.Id,
        Isbn = book.Isbn,
        IsbnKey = IsbnRules.Compact(book.Isbn),
        Title = book.Title,
        Year = book.Year,
        Author = book.Author
    };

    public static void CopyTo(Book book, BookRow row)
    {
        row.Isbn = book.Isbn;
        row.IsbnKey = IsbnRules.Compact(book.Isbn);
        row.Title = book.Title;
        row.Year = book.Year;
        row.Author = book.Author;
    }

    // ---------- BookCopy: ISBN примірника береться з батьківського рядка Books (у Copies є лише BookId).
    public static BookCopy ToDomain(CopyView view) => BookCopy.FromDto(new BookCopyDto(view.Id, view.Isbn, view.IsIssued));

    // ---------- Loan: примірник відновлюється разом з видачею, бо доменний Loan тримає посилання на BookCopy.
    public static Loan ToDomain(LoanView view) =>
        Loan.FromDto(
            new LoanDto(view.Id, view.Copy.Id, view.ReaderId, view.IssuedOn, view.ReturnedOn, view.Status.ToString(), view.LoanDays),
            ToDomain(view.Copy));

    public static LoanRow ToRow(Loan loan) => new()
    {
        Id = loan.Id,
        CopyId = loan.Copy.Id,
        ReaderId = loan.ReaderId,
        IssuedOn = loan.IssuedOn,
        ReturnedOn = loan.ReturnedOn,
        Status = loan.Status,
        LoanDays = loan.LoanDays
    };
}

// Плоскі проєкції, які EF перекладає в SELECT з JOIN (Copies ⋈ Books): тягнемо лише потрібні стовпці.
internal sealed record CopyView(string Id, string Isbn, bool IsIssued);

internal sealed record LoanView(
    string Id, string ReaderId, DateOnly IssuedOn, DateOnly? ReturnedOn, LoanStatus Status, int LoanDays, CopyView Copy);

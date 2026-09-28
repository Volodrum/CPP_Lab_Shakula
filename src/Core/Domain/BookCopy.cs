using Core.Dto;

namespace Core.Domain;

// Фізичний примірник видання: або на полиці, або виданий.
public sealed class BookCopy
{
    public string Id { get; }
    public string Isbn { get; }
    public bool IsIssued { get; private set; }

    private BookCopy(string id, string isbn, bool isIssued)
    {
        Id = id;
        Isbn = isbn;
        IsIssued = isIssued;
    }

    // Новий примірник завжди надходить на полицю.
    public static BookCopy Register(string id, string isbn) => Create(id, isbn, isIssued: false);

    private static BookCopy Create(string id, string isbn, bool isIssued)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Ідентифікатор примірника обов'язковий", nameof(id));

        string normalizedIsbn = IsbnRules.Normalize(isbn, nameof(isbn));

        return new BookCopy(id.Trim(), normalizedIsbn, isIssued);
    }

    public void Issue()
    {
        if (IsIssued)
            throw new InvalidOperationException(
                $"Примірник {Id} вже виданий, повторна видача неможлива");

        IsIssued = true;
    }

    public void Return()
    {
        if (!IsIssued)
            throw new InvalidOperationException(
                $"Примірник {Id} і так на полиці, повернути його неможливо");

        IsIssued = false;
    }

    public BookCopyDto ToDto() => new(Id, Isbn, IsIssued);

    public static BookCopy FromDto(BookCopyDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return Create(dto.Id, dto.Isbn, dto.IsIssued);
    }

    public override string ToString() => $"{Id} [{Isbn}] — {(IsIssued ? "виданий" : "на полиці")}";
}

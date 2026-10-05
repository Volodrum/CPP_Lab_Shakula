namespace Core.Domain;

// Спільна ознака сутностей, які зберігає IBookStore: кожна має унікальний Id.
// Інтерфейс не додає поведінки — лише дозволяє одному сховищу тримати Book, BookCopy і Loan.
public interface ILibraryEntity
{
    string Id { get; }
}

using Core.Dto;

namespace Core.Domain;

// Видання в каталозі. Незмінне після створення: ISBN, назва і рік — це факт про книгу.
public sealed class Book
{
    public const int MinYear = 1450;

    public string Id { get; }
    public string Isbn { get; }
    public string Title { get; }
    public int Year { get; }
    public string? Author { get; }

    private Book(string id, string isbn, string title, int year, string? author)
    {
        Id = id;
        Isbn = isbn;
        Title = title;
        Year = year;
        Author = author;
    }

    // Єдиний спосіб створити книгу: усі перевірки тут.
    public static Book Create(string id, string isbn, string title, int year, string? author = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Ідентифікатор книги обов'язковий", nameof(id));

        string normalizedIsbn = IsbnRules.Normalize(isbn, nameof(isbn));

        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Назва книги не може бути порожньою", nameof(title));

        int currentYear = DateTime.Today.Year;
        if (year < MinYear || year > currentYear)
            throw new ArgumentOutOfRangeException(nameof(year), year,
                $"Рік видання має бути в межах {MinYear}..{currentYear}");

        return new Book(id.Trim(), normalizedIsbn, title.Trim(), year,
            string.IsNullOrWhiteSpace(author) ? null : author.Trim());
    }

    // Мапінг у формат тижня 3 і назад — знадобиться сховищу тижня 5.
    public BookDto ToDto() => new(Id, Isbn, Title, Year, Author);

    public static Book FromDto(BookDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        return Create(dto.Id, dto.Isbn, dto.Title, dto.Year, dto.Author);
    }

    public override string ToString() =>
        $"{Id} [{Isbn}] «{Title}», {Year}" + (Author is null ? "" : $" — {Author}");
}

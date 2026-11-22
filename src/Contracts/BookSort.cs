namespace CrossApp.Contracts;

// Тиждень 12 (дод. завдання 2): допустимі значення параметра ?sort= у GET /api/books.
// Сортує сервер (до пагінації), клієнт лише передає вибір користувача. Мінус на початку — спадання.
// Список у спільному проєкті, щоб випадаючий список у Web і валідатор в Api не розійшлися.
public static class BookSort
{
    public const string Title = "title";
    public const string Author = "author";
    public const string Year = "year";
    public const string Copies = "copies";

    public const string Default = Title;

    public static readonly IReadOnlyList<string> Allowed =
    [
        Title, "-" + Title,
        Author, "-" + Author,
        Year, "-" + Year,
        Copies, "-" + Copies
    ];

    public static bool IsAllowed(string? sort) =>
        string.IsNullOrEmpty(sort) || Allowed.Contains(sort, StringComparer.OrdinalIgnoreCase);
}

using Core.Domain;
using Core.Dto;

namespace Core.Tests;

// Маленький фіксований фонд саме для тестів (не SampleData: її зміна не повинна ламати тести).
// Кожен виклик створює нові об'єкти — тести не ділять стан.
//
//   видання   примірників   на полиці
//   B-BIG          5             4      (один виданий)
//   B-MID          3             3
//   B-LOW          1             0      (єдиний виданий)
//   B-NONE         0             0
internal static class TestData
{
    public const string BigIsbn = "978-0-00-000001-0";
    public const string MidIsbn = "978-0-00-000002-0";
    public const string LowIsbn = "978-0-00-000003-0";
    public const string NoneIsbn = "978-0-00-000004-0";

    public static IReadOnlyList<Book> Books() =>
    [
        Book.Create("B-BIG", BigIsbn, "Велике видання", 2001),
        Book.Create("B-MID", MidIsbn, "Середнє видання", 1999),
        Book.Create("B-LOW", LowIsbn, "Рідкісне видання", 1988),
        Book.Create("B-NONE", NoneIsbn, "Списане видання", 1975)
    ];

    public static IReadOnlyList<BookCopy> Copies() =>
    [
        Copy("C-1", BigIsbn, issued: true),
        Copy("C-2", BigIsbn),
        Copy("C-3", BigIsbn),
        Copy("C-4", BigIsbn),
        Copy("C-5", BigIsbn),
        Copy("C-6", MidIsbn),
        Copy("C-7", MidIsbn),
        Copy("C-8", MidIsbn),
        Copy("C-9", LowIsbn, issued: true)
    ];

    private static BookCopy Copy(string id, string isbn, bool issued = false) =>
        BookCopy.FromDto(new BookCopyDto(id, isbn, issued));
}

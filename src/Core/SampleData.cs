using Core.Domain;

namespace Core;

// Початкові дані для InMemoryBookStore і для LINQ-запитів тижня 7: 15 видань, 20 примірників, 4 видачі.
// Кожен виклик створює нові об'єкти, тож два сховища не ділять між собою стан.
public static class SampleData
{
    public static IReadOnlyList<ILibraryEntity> Items()
    {
        Book[] books =
        [
            Book.Create("B-001", "978-0-201-61622-4", "Прагматичний програміст", 1999, "Ендрю Гант"),
            Book.Create("B-002", "978-0-201-63361-0", "Design Patterns", 1994, "Еріх Гамма"),
            Book.Create("B-003", "978-0-13-468599-1", "Effective Java", 2018, "Джошуа Блох"),
            Book.Create("B-004", "978-1-61729-453-2", "C# in Depth", 2019, "Джон Скіт"),
            Book.Create("B-005", "978-0-321-12521-7", "Domain-Driven Design", 2003, "Ерік Еванс"),
            Book.Create("B-006", "978-0-13-449416-6", "Чиста архітектура", 2017, "Роберт Мартін"),
            Book.Create("B-007", "978-0-596-00712-6", "Head First Design Patterns", 2004, "Ерік Фрімен"),
            Book.Create("B-008", "978-0-262-03384-8", "Вступ до алгоритмів", 2009, "Томас Кормен"),
            Book.Create("B-009", "978-0-13-110362-7", "Мова програмування C", 1988, "Браян Керніган"),
            Book.Create("B-010", "978-0-201-48567-7", "Рефакторинг", 1999, "Мартін Фаулер"),
            Book.Create("B-011", "978-0-7356-1967-8", "Code Complete", 2004, "Стів Макконнелл"),
            Book.Create("B-012", "978-0-201-83595-3", "Міфічний людино-місяць", 1995, "Фредерік Брукс"),
            Book.Create("B-013", "978-0-13-117705-5", "Working Effectively with Legacy Code", 2004, "Майкл Фезерс"),
            Book.Create("B-014", "978-0-321-14653-3", "Test-Driven Development", 2002, "Кент Бек"),
            Book.Create("B-015", "978-0-7356-6745-7", "CLR via C#", 2012)
        ];

        // Примірники: у популярних видань по два, у решти — по одному (разом 20).
        int[] twoCopies = [0, 2, 3, 5, 9];
        var copies = new List<BookCopy>();
        for (int i = 0; i < books.Length; i++)
        {
            copies.Add(BookCopy.Register($"C-{copies.Count + 1:000}", books[i].Isbn));
            if (twoCopies.Contains(i))
                copies.Add(BookCopy.Register($"C-{copies.Count + 1:000}", books[i].Isbn));
        }

        var day = new DateOnly(2026, 9, 1);
        Loan open = Loan.Open("L-001", copies[0], "R-001", day);
        Loan lost = Loan.Open("L-002", copies[4], "R-002", day.AddDays(3));
        lost.MarkLost();
        Loan closed = Loan.Open("L-003", copies[7], "R-001", day.AddDays(5));
        closed.Close(day.AddDays(19));
        Loan second = Loan.Open("L-004", copies[12], "R-003", day.AddDays(20));

        return [.. books, .. copies, open, lost, closed, second];
    }
}

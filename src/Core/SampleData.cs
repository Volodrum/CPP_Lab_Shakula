using Core.Domain;

namespace Core;

// Початкові дані для InMemoryBookStore і LINQ-звітів тижня 7: 19 видань (п'ять десятиліть), 52 примірники
// (від 1 до 5 на видання), 21 видача семи читачам (відкриті, закриті, одна втрачена; частина прострочена).
// Кожен виклик створює нові об'єкти, тож два сховища не ділять між собою стан.
public static class SampleData
{
    public static IReadOnlyList<ILibraryEntity> Items()
    {
        // (видання, кількість примірників). Рівні кількості (5/5, 4/4/4/4) — щоб топ-N мав що розрізняти другим ключем.
        (Book Book, int Copies)[] catalog =
        [
            (Book.Create("B-001", "978-0-201-61622-4", "Прагматичний програміст", 1999, "Ендрю Гант"), 4),
            (Book.Create("B-002", "978-0-201-63361-0", "Design Patterns", 1994, "Еріх Гамма"), 4),
            (Book.Create("B-003", "978-0-13-468599-1", "Effective Java", 2018, "Джошуа Блох"), 3),
            (Book.Create("B-004", "978-1-61729-453-2", "C# in Depth", 2019, "Джон Скіт"), 5),
            (Book.Create("B-005", "978-0-321-12521-7", "Domain-Driven Design", 2003, "Ерік Еванс"), 2),
            (Book.Create("B-006", "978-0-13-449416-6", "Чиста архітектура", 2017, "Роберт Мартін"), 4),
            (Book.Create("B-007", "978-0-596-00712-6", "Head First Design Patterns", 2004, "Ерік Фрімен"), 2),
            (Book.Create("B-008", "978-0-262-03384-8", "Вступ до алгоритмів", 2009, "Томас Кормен"), 3),
            (Book.Create("B-009", "978-0-13-110362-7", "Мова програмування C", 1988, "Браян Керніган"), 1),
            (Book.Create("B-010", "978-0-201-48567-7", "Рефакторинг", 1999, "Мартін Фаулер"), 3),
            (Book.Create("B-011", "978-0-7356-1967-8", "Code Complete", 2004, "Стів Макконнелл"), 2),
            (Book.Create("B-012", "978-0-201-83595-3", "Міфічний людино-місяць", 1995, "Фредерік Брукс"), 1),
            (Book.Create("B-013", "978-0-13-117705-5", "Working Effectively with Legacy Code", 2004, "Майкл Фезерс"), 1),
            (Book.Create("B-014", "978-0-321-14653-3", "Test-Driven Development", 2002, "Кент Бек"), 2),
            (Book.Create("B-015", "978-0-7356-6745-7", "CLR via C#", 2012), 5),
            (Book.Create("B-016", "978-1-4842-0077-3", "Pro Git", 2014, "Скотт Чакон"), 2),
            (Book.Create("B-017", "978-0-262-51087-5", "Структура та інтерпретація програм", 1996, "Гарольд Абельсон"), 1),
            (Book.Create("B-018", "978-1-4493-7332-0", "Designing Data-Intensive Applications", 2017, "Мартін Клеппман"), 3),
            (Book.Create("B-019", "978-1-0981-4744-0", "C# 12 in a Nutshell", 2023, "Джозеф Албахарі"), 4)
        ];

        var copies = new List<BookCopy>();
        var copiesOf = new Dictionary<string, List<BookCopy>>();
        foreach (var (book, count) in catalog)
        {
            copiesOf[book.Id] = [];
            for (int i = 0; i < count; i++)
            {
                var copy = BookCopy.Register($"C-{copies.Count + 1:000}", book.Isbn);
                copies.Add(copy);
                copiesOf[book.Id].Add(copy);
            }
        }

        var day = new DateOnly(2026, 9, 1);
        var loans = new List<Loan>();

        // closedAfter: null — видача відкрита; число — закрито через стільки днів. Строк — 14 днів (Loan.LoanPeriodDays).
        void Lend(string bookId, int copyNo, string readerId, int issuedDay, int? closedAfter = null, bool lost = false)
        {
            DateOnly issuedOn = day.AddDays(issuedDay);
            Loan loan = Loan.Open($"L-{loans.Count + 1:000}", copiesOf[bookId][copyNo], readerId, issuedOn);
            if (lost) loan.MarkLost();
            if (closedAfter is int days) loan.Close(issuedOn.AddDays(days));
            loans.Add(loan);
        }

        Lend("B-001", 0, "R-001", 0);                   // 01.09, строк 15.09
        Lend("B-004", 0, "R-002", 3, lost: true);       // втрачено, за читачем досі
        Lend("B-006", 0, "R-001", 5, closedAfter: 14);
        Lend("B-010", 0, "R-003", 20);                  // строк 05.10
        Lend("B-003", 0, "R-001", 7, closedAfter: 10);
        Lend("B-008", 0, "R-002", 9, closedAfter: 14);
        Lend("B-015", 0, "R-003", 11, closedAfter: 13);
        Lend("B-002", 0, "R-004", 14);                  // строк 29.09
        Lend("B-005", 0, "R-001", 21, closedAfter: 9);
        Lend("B-011", 0, "R-002", 24);                  // строк 09.10
        Lend("B-012", 0, "R-005", 27, closedAfter: 12);
        Lend("B-006", 1, "R-003", 30);                  // строк 15.10
        Lend("B-014", 0, "R-004", 31, closedAfter: 10);
        Lend("B-019", 0, "R-001", 35);                  // 06.10, строк 20.10
        Lend("B-016", 0, "R-006", 36);
        Lend("B-018", 0, "R-002", 38);
        Lend("B-009", 0, "R-003", 39);                  // єдиний примірник — на полиці 0
        Lend("B-004", 1, "R-004", 41);
        Lend("B-007", 0, "R-005", 42);
        Lend("B-013", 0, "R-006", 43);                  // єдиний примірник
        Lend("B-017", 0, "R-007", 2);                   // єдиний примірник, строк 17.09

        return [.. catalog.Select(c => c.Book), .. copies, .. loans];
    }

    // Дод. завдання 4: синтетичний фонд для вимірювань — count видань, від 1 до 4 примірників у кожного,
    // кожен третій примірник виданий. ISBN-13 = "978" + 10 цифр номера.
    public static (IReadOnlyList<Book> Books, IReadOnlyList<BookCopy> Copies) Generate(int count)
    {
        var books = new List<Book>(count);
        var copies = new List<BookCopy>(count * 5 / 2);
        for (int i = 0; i < count; i++)
        {
            Book book = Book.Create($"G-{i:000000}", $"978{i:0000000000}", $"Видання {i}", 1950 + i % 76);
            books.Add(book);
            for (int c = 0; c <= i % 4; c++)
            {
                var copy = BookCopy.Register($"GC-{copies.Count:0000000}", book.Isbn);
                if (copies.Count % 3 == 0) copy.Issue();
                copies.Add(copy);
            }
        }
        return (books, copies);
    }
}

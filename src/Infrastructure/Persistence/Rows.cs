namespace Infrastructure.Persistence;

// Моделі зберігання (persistence models) — не доменні сутності. Публічні сетери й навігаційні властивості тут
// потрібні EF Core, щоб матеріалізувати рядок таблиці й відстежувати зміни. Доменні Book / BookCopy / Loan
// лишаються незмінними (приватні конструктори, фабрики з перевірками) і не знають про EF взагалі.
// Варіант «Бібліотека»: Book 1:N Copy (інвентарний номер, чи виданий), унікальний ISBN; Copy 1:N Loan — журнал видач.

// Таблиця Books — видання.
public sealed class BookRow
{
    public string Id { get; set; } = "";

    // ISBN так, як його ввели (з дефісами чи без) — для показу.
    public string Isbn { get; set; } = "";

    // Той самий ISBN без дефісів і пробілів — по ньому унікальний індекс: «978-0-13-235088-4» і «9780132350884»
    // для БД одне видання, як і для Book.HasIsbn у домені.
    public string IsbnKey { get; set; } = "";

    public string Title { get; set; } = "";
    public int Year { get; set; }
    public string? Author { get; set; }

    public List<CopyRow> Copies { get; set; } = [];
}

// Таблиця Copies — фізичні примірники. Дочірня до Books: зовнішній ключ BookId, каскадне видалення.
public sealed class CopyRow
{
    // Інвентарний номер примірника (C-001), він же первинний ключ.
    public string Id { get; set; } = "";

    public string BookId { get; set; } = "";
    public bool IsIssued { get; set; }

    // Коли примірник надійшов до фонду. Лише в БД (як ReceivedAt у партіях складу): домен цієї дати не знає,
    // а для журналу надходжень вона корисна. Пише сховище в момент Add.
    public DateTimeOffset RegisteredAt { get; set; }

    public BookRow Book { get; set; } = null!;
    public List<LoanRow> Loans { get; set; } = [];
}

// Таблиця Loans (міграція AddLoans) — видачі примірників читачам. Видалення примірника з історією заборонене (Restrict).
public sealed class LoanRow
{
    public string Id { get; set; } = "";
    public string CopyId { get; set; } = "";
    public string ReaderId { get; set; } = "";
    public DateOnly IssuedOn { get; set; }
    public DateOnly? ReturnedOn { get; set; }
    public Core.Domain.LoanStatus Status { get; set; }
    public int LoanDays { get; set; }

    public CopyRow Copy { get; set; } = null!;
}

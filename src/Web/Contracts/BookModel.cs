using System.ComponentModel.DataAnnotations;

namespace Web.Contracts;

// Модель форми видання (EditForm). Це не DTO сервера, а стан полів вводу: змінний клас із сетерами,
// бо InputText/InputNumber пишуть у властивості. Назви властивостей збігаються з ключами помилок Api
// (Isbn, Title, Author, Year, Copies) — так 400 ValidationProblem лягає біля потрібного поля.
//
// Data Annotations тут — лише для зручності: прості перевірки «порожньо / задовго / від'ємно» ще до запиту.
// Справжні правила (контрольна сума ISBN, рік ≤ поточного, унікальність ISBN) живуть у Api та Core:
// клієнтську перевірку можна вимкнути або обійти будь-яким curl, тож вона не замінює серверну.
public sealed class BookModel
{
    public string Id { get; set; } = "";

    [Required(ErrorMessage = "ISBN обов'язковий.")]
    [StringLength(20, ErrorMessage = "ISBN — до 20 символів.")]
    public string Isbn { get; set; } = "";

    [Required(ErrorMessage = "Назва обов'язкова.")]
    [StringLength(200, ErrorMessage = "Назва — до 200 символів.")]
    public string Title { get; set; } = "";

    [StringLength(120, ErrorMessage = "Автор — до 120 символів.")]
    public string? Author { get; set; }

    // Межі року навмисно не дублюються: «не раніше Book.MinYear і не пізніше поточного» перевіряє сервер.
    public int Year { get; set; } = DateTime.Today.Year;

    [Range(0, 100, ErrorMessage = "Кількість примірників — від 0 до 100.")]
    public int Copies { get; set; } = 1;

    // Лише для показу: скільки примірників на полиці (з відповіді Api, у запит не йде).
    public int Available { get; set; }

    public bool IsNew => string.IsNullOrEmpty(Id);

    public static BookModel FromDto(BookDto dto) => new()
    {
        Id = dto.Id,
        Isbn = dto.Isbn,
        Title = dto.Title,
        Author = dto.Author,
        Year = dto.Year,
        Copies = dto.Copies,
        Available = dto.Available
    };

    // Порожній автор → null, як очікує контракт (author — необов'язкове поле).
    public SaveBookRequest ToRequest() =>
        new(Isbn.Trim(), Title.Trim(), string.IsNullOrWhiteSpace(Author) ? null : Author.Trim(), Year, Copies);
}

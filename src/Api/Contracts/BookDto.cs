namespace Api.Contracts;

// Вихідний DTO: так видання виглядає для клієнтів API (curl, Scalar, Blazor з тижня 12).
// Назви й склад полів — частина HTTP-контракту: їх не змінюємо разом із доменом.
// Не плутати з Core.Dto.BookDto — то формат файлу сховища (з полем "kind"), а не відповідь API.
//   copies    — скільки примірників видання є у фонді;
//   available — скільки з них зараз на полиці (не видано).
public sealed record BookDto(
    string Id,
    string Isbn,
    string Title,
    string? Author,
    int Year,
    int Copies,
    int Available);

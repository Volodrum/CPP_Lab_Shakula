namespace CrossApp.Contracts;

// Вихідний DTO: так видання виглядає для клієнтів API (curl, Scalar, Blazor-клієнт src/Web).
// Назви й склад полів — частина HTTP-контракту: їх не змінюємо разом із доменом.
// Не плутати з Core.Dto.BookDto — то формат файлу сховища (з полем "kind"), а не відповідь API.
//   copies    — скільки примірників видання є у фонді;
//   available — скільки з них зараз на полиці (не видано).
// Тиждень 12 (дод. завдання 3): перенесено з src/Api/Contracts у спільний проєкт src/Contracts,
// щоб Api і Web компілювалися проти одного й того самого опису JSON.
public sealed record BookDto(
    string Id,
    string Isbn,
    string Title,
    string? Author,
    int Year,
    int Copies,
    int Available);

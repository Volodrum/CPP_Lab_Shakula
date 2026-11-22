namespace Web.Contracts;

// Результат зміни через Api (POST/PUT/DELETE) у формі, зручній для сторінки:
//   Ok        — 2xx; Book — збережене видання з відповіді (для DELETE — null);
//   Message   — загальне повідомлення (404, 409, 5xx, недоступний Api, загальний текст 400);
//   Errors    — словник «поле → повідомлення» з 400 ValidationProblem; показується біля полів форми;
//   Status    — HTTP-код (null — відповіді не було взагалі: Api не запущений, мережа, тайм-аут).
// Сторінка не бачить HttpResponseMessage і не розбирає JSON помилок сама — це робить BooksApiClient.
public sealed record ApiResult(
    bool Ok,
    string? Message = null,
    Dictionary<string, string[]>? Errors = null,
    int? Status = null,
    BookDto? Book = null)
{
    public bool IsConflict => Status == 409;
    public bool IsNotFound => Status == 404;
}

// Тіло помилки Api у форматі ProblemDetails (RFC 9457): 400 — з errors, 409 — з detail.
// Читаємо лише потрібні поля; System.Net.Http.Json зіставляє "title"/"errors" з Title/Errors без атрибутів.
public sealed record ValidationProblemResponse(
    string? Title,
    string? Detail,
    int? Status,
    Dictionary<string, string[]>? Errors);

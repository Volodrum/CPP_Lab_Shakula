using Microsoft.AspNetCore.Mvc;

namespace Api.Contracts;

// Параметри рядка запиту GET /api/books (через [AsParameters]): ?author=...&search=...&page=1&pageSize=20.
//   author — частина імені автора (варіант «Бібліотека»), без урахування регістру;
//   search — частина назви, автора або ISBN (дод. завдання 1);
//   page, pageSize — пагінація (дод. завдання 1), за замовчуванням 1 і 20.
// Name = camelCase — щоб в OpenAPI/Scalar параметри звалися так само, як поля JSON.
public sealed record BookListQuery(
    [FromQuery(Name = "author")] string? Author = null,
    [FromQuery(Name = "search")] string? Search = null,
    [FromQuery(Name = "page")] int Page = 1,
    [FromQuery(Name = "pageSize")] int PageSize = 20);

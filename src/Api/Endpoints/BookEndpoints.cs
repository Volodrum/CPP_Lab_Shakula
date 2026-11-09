using Api.Contracts;
using Api.Validation;
using Core.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.OpenApi;

namespace Api.Endpoints;

// П'ять маршрутів CRUD над каталогом бібліотеки. Обробники тонкі: зв'язати параметри → викликати Core → вибрати
// HTTP-результат. Валідація — у фільтрі (до обробника), DomainException → 409 — в ApiExceptionHandler.
// Results<...> перелічує всі можливі відповіді: компілятор не дасть повернути щось поза списком,
// а OpenAPI бере звідси коди і схеми для Scalar.
public static class BookEndpoints
{
    public const string Route = "/api/books";

    public static RouteGroupBuilder MapBooks(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup(Route)
            .WithTags("Books")
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                // У .NET 10 замість застарілого .WithOpenApi() (ASPDEPR002): спільний опис 500 для всіх операцій групи.
                operation.Responses ??= new OpenApiResponses();
                operation.Responses.TryAdd("500", new OpenApiResponse
                {
                    Description = "Неочікувана помилка: ProblemDetails без технічних деталей, лише traceId."
                });
                return Task.CompletedTask;
            });

        // GET /api/books?author=&search=&page=1&pageSize=20 — список з фільтрами й пагінацією.
        group.MapGet("/", async Task<Ok<PagedResponse<BookDto>>> (
                [AsParameters] BookListQuery query, CatalogService catalog, CancellationToken ct) =>
            {
                CatalogPage page = await catalog.SearchAsync(query.ToQuery(), ct);
                return TypedResults.Ok(page.ToResponse());
            })
            .Validate<BookListQuery>()
            .WithName("ListBooks")
            .WithSummary("Список видань")
            .WithDescription("Фільтри author (частина імені автора) і search (назва, автор або ISBN); " +
                             "пагінація page/pageSize. Відповідь: { items, total, page, pageSize }.");

        // GET /api/books/{id} — одне видання або 404. Без обмеження типу: id — короткий рядок "B-001".
        group.MapGet("/{id}", async Task<Results<Ok<BookDto>, NotFound>> (
                string id, CatalogService catalog, CancellationToken ct) =>
                await catalog.GetAsync(id, ct) is { } entry
                    ? TypedResults.Ok(entry.ToDto())
                    : TypedResults.NotFound())
            .WithName("GetBook")
            .WithSummary("Видання за id");

        // POST /api/books — нове видання з copies примірниками: 201 + Location на GET /api/books/{id}.
        group.MapPost("/", async Task<Created<BookDto>> (
                SaveBookRequest request, CatalogService catalog, CancellationToken ct) =>
            {
                CatalogEntry created = await catalog.CreateAsync(request.ToDraft(), ct);
                return TypedResults.Created($"{Route}/{created.Book.Id}", created.ToDto());
            })
            .Validate<SaveBookRequest>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("CreateBook")
            .WithSummary("Додати видання")
            .WithDescription("409 — видання з таким ISBN уже є.");

        // PUT /api/books/{id} — повна заміна. Те саме тіло вдруге дає той самий стан (ідемпотентність).
        group.MapPut("/{id}", async Task<Results<Ok<BookDto>, NotFound>> (
                string id, SaveBookRequest request, CatalogService catalog, CancellationToken ct) =>
                await catalog.ReplaceAsync(id, request.ToDraft(), ct) is { } replaced
                    ? TypedResults.Ok(replaced.ToDto())
                    : TypedResults.NotFound())
            .Validate<SaveBookRequest>()
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("ReplaceBook")
            .WithSummary("Замінити видання")
            .WithDescription("copies — бажана кількість примірників: бракує — додаються, зайві вільні — вилучаються. " +
                             "409 — ISBN зайнятий іншим виданням, зміна ISBN при наявних примірниках " +
                             "або вилучення виданих примірників.");

        // DELETE /api/books/{id} — 204; повторний DELETE — 404 (ресурсу вже немає).
        group.MapDelete("/{id}", async Task<Results<NoContent, NotFound>> (
                string id, CatalogService catalog, CancellationToken ct) =>
                await catalog.DeleteAsync(id, ct)
                    ? TypedResults.NoContent()
                    : TypedResults.NotFound())
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithName("DeleteBook")
            .WithSummary("Вилучити видання")
            .WithDescription("Разом із вільними примірниками. 409 — є видані примірники або історія видач.");

        return group;
    }
}

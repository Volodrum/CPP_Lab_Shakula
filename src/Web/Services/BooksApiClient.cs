using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Web.Contracts;

namespace Web.Services;

// Типізований клієнт Api (реєструється AddHttpClient<BooksApiClient> у Program.cs, базова адреса — з Api:BaseUrl).
// Він знає лише маршрути /api/books і форму JSON; ні БД, ні доменних правил тут немає.
//
// Дві групи методів:
//   читання (List/Find/Ping) — повертають дані або кидають ApiUnavailableException / HttpRequestException;
//     404 у FindAsync — не виняток, а null («такого видання немає»);
//   зміни (Create/Update/Delete) — завжди повертають ApiResult: сторінці зручно показати 400 біля полів,
//     409 — загальним повідомленням, а недоступний Api — текстом, без try/catch у кожній формі.
// CancellationToken передається до HttpClient: якщо користувач пішов зі сторінки, запит до Api скасовується.
public sealed class BooksApiClient(HttpClient http)
{
    public const string Unavailable = "Api недоступний. Перевірте, чи запущено src/Api.";
    public const string ValidationFailed = "Дані не пройшли перевірку.";
    public const string NotFoundText = "Запис не знайдено — можливо, його вже видалили.";

    private const string Books = "api/books";

    public Uri? BaseAddress => http.BaseAddress;

    public Task<PagedResponse<BookDto>> ListAsync(BookListRequest request, CancellationToken ct = default) =>
        Guard(async () =>
        {
            using var response = await http.GetAsync(request.ToRelativeUrl(), ct);
            await EnsureSuccessAsync(response, ct);
            return await response.Content.ReadFromJsonAsync<PagedResponse<BookDto>>(ct)
                   ?? new PagedResponse<BookDto>([], 0, request.Page, request.PageSize);
        }, ct);

    // GetAsync + перевірка 404 → null: «немає такого» — нормальний результат, а не збій.
    public Task<BookDto?> FindAsync(string id, CancellationToken ct = default) =>
        Guard(async () =>
        {
            using var response = await http.GetAsync($"{Books}/{Uri.EscapeDataString(id)}", ct);
            if (response.StatusCode == HttpStatusCode.NotFound)
                return null;
            await EnsureSuccessAsync(response, ct);
            return await response.Content.ReadFromJsonAsync<BookDto>(ct);
        }, ct);

    // GET /health — для індикатора на головній сторінці.
    public async Task<bool> PingAsync(CancellationToken ct = default)
    {
        try
        {
            using var response = await http.GetAsync("health", ct);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException) { return false; }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return false; } // тайм-аут
    }

    public Task<ApiResult> CreateAsync(BookModel model, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Post, Books, model, ct);

    public Task<ApiResult> UpdateAsync(BookModel model, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Put, $"{Books}/{Uri.EscapeDataString(model.Id)}", model, ct);

    // Без id — створення (POST), з id — заміна (PUT). Так одна форма обслуговує обидва сценарії.
    public Task<ApiResult> SaveAsync(BookModel model, CancellationToken ct = default) =>
        model.IsNew ? CreateAsync(model, ct) : UpdateAsync(model, ct);

    public Task<ApiResult> DeleteAsync(string id, CancellationToken ct = default) =>
        SendAsync(HttpMethod.Delete, $"{Books}/{Uri.EscapeDataString(id)}", model: null, ct);

    private async Task<ApiResult> SendAsync(HttpMethod method, string url, BookModel? model, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(method, url);
            if (model is not null)
                request.Content = JsonContent.Create(model.ToRequest()); // camelCase, як очікує Api
            using var response = await http.SendAsync(request, ct);
            int status = (int)response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                BookDto? book = response.StatusCode == HttpStatusCode.NoContent
                    ? null
                    : await response.Content.ReadFromJsonAsync<BookDto>(ct);
                return new ApiResult(true, Status: status, Book: book);
            }

            ValidationProblemResponse? problem = await ReadProblemAsync(response, ct);
            return response.StatusCode switch
            {
                HttpStatusCode.NotFound => new ApiResult(false, NotFoundText, Status: status),
                // 400: словник errors — біля полів; загальний текст — над формою.
                HttpStatusCode.BadRequest => new ApiResult(false, ValidationFailed, problem?.Errors, status),
                // 409: доменне правило (дубль ISBN, видані примірники) — не поле форми, а загальне повідомлення.
                HttpStatusCode.Conflict => new ApiResult(false,
                    problem?.Detail ?? problem?.Title ?? "Операція суперечить правилам бібліотеки.", Status: status),
                _ => new ApiResult(false, $"Сервер повернув {status}. {problem?.Title}".TrimEnd(), Status: status)
            };
        }
        catch (HttpRequestException) // Api не запущений або недоступний по мережі
        {
            return new ApiResult(false, Unavailable);
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) // спрацював HttpClient.Timeout
        {
            return new ApiResult(false, Unavailable);
        }
    }

    // Читання: мережеві збої й тайм-аут → ApiUnavailableException з людським текстом;
    // скасування користувачем (ct) пролітає як OperationCanceledException — сторінка його просто ігнорує.
    private static async Task<T> Guard<T>(Func<Task<T>> call, CancellationToken ct)
    {
        try
        {
            return await call();
        }
        catch (HttpRequestException ex) when (ex.StatusCode is null)
        {
            throw new ApiUnavailableException(Unavailable, ex);
        }
        catch (TaskCanceledException ex) when (!ct.IsCancellationRequested)
        {
            throw new ApiUnavailableException("Api не відповів вчасно (Api:TimeoutSeconds).", ex);
        }
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken ct)
    {
        if (response.IsSuccessStatusCode)
            return;
        ValidationProblemResponse? problem = await ReadProblemAsync(response, ct);
        string details = problem?.Errors is { Count: > 0 } errors
            ? " " + string.Join(" ", errors.SelectMany(e => e.Value.Select(m => $"{e.Key}: {m}")))
            : problem?.Detail is { } detail ? " " + detail : "";
        throw new HttpRequestException($"Сервер повернув {(int)response.StatusCode}.{details}", null, response.StatusCode);
    }

    // Тіло помилки — ProblemDetails; якщо там не JSON (проксі, HTML-сторінка), повертаємо null, а не падаємо.
    private static async Task<ValidationProblemResponse?> ReadProblemAsync(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            return await response.Content.ReadFromJsonAsync<ValidationProblemResponse>(ct);
        }
        catch (JsonException) { return null; }
        catch (NotSupportedException) { return null; } // порожнє тіло без Content-Type
    }
}

// Api не відповів зовсім: не запущений, інший порт, мережа, тайм-аут. Message — готовий текст для користувача.
public sealed class ApiUnavailableException(string message, Exception inner) : Exception(message, inner);

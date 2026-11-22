using System.Net;
using System.Text;
using System.Text.Json;
using Web.Services;

namespace Web.Tests;

// Підставний Api для тестів клієнта і сторінок: замість мережі — делегат, що відповідає на HttpRequestMessage.
// Запам'ятовує всі запити (метод, адреса, тіло), щоб тест міг перевірити, що саме надіслав Web.
public sealed class StubApi : HttpMessageHandler
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);
    private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _respond;

    public StubApi(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond) => _respond = respond;

    public StubApi(Func<HttpRequestMessage, HttpResponseMessage> respond) : this((r, _) => Task.FromResult(respond(r))) { }

    public List<(HttpMethod Method, string Url, string? Body)> Requests { get; } = [];

    // Базова адреса — як у конфігурації Web, тільки вигадана: справжньої мережі тут немає.
    public BooksApiClient Client(TimeSpan? timeout = null) =>
        new(new HttpClient(this) { BaseAddress = new Uri("http://api.test/"), Timeout = timeout ?? TimeSpan.FromSeconds(5) });

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
        lock (Requests)
            Requests.Add((request.Method, request.RequestUri!.PathAndQuery, body));
        return await _respond(request, ct);
    }

    public static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK, string mediaType = "application/json") =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(value, Web), Encoding.UTF8, mediaType) };

    public static HttpResponseMessage Page(params BookDto[] items) =>
        Json(new PagedResponse<BookDto>(items, items.Length, 1, 10));

    // 400 у форматі, який повертає Api (TypedResults.ValidationProblem).
    public static HttpResponseMessage ValidationProblem(Dictionary<string, string[]> errors) =>
        Json(new { title = "One or more validation errors occurred.", status = 400, errors },
            HttpStatusCode.BadRequest, "application/problem+json");

    public static HttpResponseMessage Conflict(string detail) =>
        Json(new { title = "Операція суперечить правилам бібліотеки.", status = 409, detail },
            HttpStatusCode.Conflict, "application/problem+json");

    public static HttpResponseMessage Status(HttpStatusCode status) => new(status);

    public static BookDto Book(string id = "B-1", string title = "Кобзар", int copies = 2, int available = 2) =>
        new(id, "978-617-12-3456-7", title, "Тарас Шевченко", 1840, copies, available);
}

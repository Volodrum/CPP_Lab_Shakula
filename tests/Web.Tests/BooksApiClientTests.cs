using System.Net;
using System.Text.Json;
using Web.Contracts;
using Web.Services;

namespace Web.Tests;

// Типізований клієнт: як HTTP-відповіді Api перетворюються на те, що бачить сторінка.
public class BooksApiClientTests
{
    private static BookModel ValidModel(string id = "") => new()
    {
        Id = id, Isbn = "978-617-12-3456-7", Title = "Кобзар", Author = "Тарас Шевченко", Year = 1840, Copies = 2
    };

    [Fact] // фільтр, сортування і сторінка йдуть в Api query-параметрами — фільтрує сервер, а не UI
    public async Task ListAsync_SendsQueryParameters_AndReadsPagedResponse()
    {
        var api = new StubApi(_ => StubApi.Page(StubApi.Book()));

        var page = await api.Client().ListAsync(new BookListRequest("Шевченко", "-year", Page: 2, PageSize: 5));

        Assert.Equal("Кобзар", Assert.Single(page.Items).Title);
        string url = Uri.UnescapeDataString(Assert.Single(api.Requests).Url);
        Assert.Equal("/api/books?author=Шевченко&sort=-year&page=2&pageSize=5", url);
    }

    [Fact] // JSON запиту — camelCase, без id (його бере маршрут) і без available
    public async Task CreateAsync_PostsCamelCaseJson_AndReturnsCreatedBook()
    {
        var api = new StubApi(_ => StubApi.Json(StubApi.Book("B-new"), HttpStatusCode.Created));

        ApiResult result = await api.Client().CreateAsync(ValidModel());

        Assert.True(result.Ok);
        Assert.Equal("B-new", result.Book?.Id);
        var (method, url, body) = Assert.Single(api.Requests);
        Assert.Equal((HttpMethod.Post, "/api/books"), (method, url));
        using var json = JsonDocument.Parse(body!);
        Assert.Equal(["isbn", "title", "author", "year", "copies"], json.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal("Кобзар", json.RootElement.GetProperty("title").GetString());
    }

    [Fact] // модель з id — PUT на /api/books/{id}
    public async Task SaveAsync_WithId_SendsPut()
    {
        var api = new StubApi(_ => StubApi.Json(StubApi.Book()));

        await api.Client().SaveAsync(ValidModel("B-1"));

        Assert.Equal((HttpMethod.Put, "/api/books/B-1"), (api.Requests[0].Method, api.Requests[0].Url));
    }

    [Fact] // 400 ValidationProblem → загальний текст + словник помилок за полями
    public async Task Save_400_ReturnsFieldErrors()
    {
        var api = new StubApi(_ => StubApi.ValidationProblem(new()
        {
            ["Isbn"] = ["ISBN обов'язковий."],
            ["Copies"] = ["Кількість примірників має бути від 0 до 100."]
        }));

        ApiResult result = await api.Client().CreateAsync(new BookModel { Copies = -1 });

        Assert.False(result.Ok);
        Assert.Equal(BooksApiClient.ValidationFailed, result.Message);
        Assert.Equal(["Copies", "Isbn"], result.Errors!.Keys.Order());
        Assert.Equal(400, result.Status);
    }

    [Fact] // 409 → detail конфлікту загальним повідомленням, без errors
    public async Task Save_409_ReturnsConflictDetail()
    {
        var api = new StubApi(_ => StubApi.Conflict("Видання з ISBN 978-617-12-3456-7 уже є в каталозі: B-1."));

        ApiResult result = await api.Client().CreateAsync(ValidModel());

        Assert.True(result.IsConflict);
        Assert.Null(result.Errors);
        Assert.Contains("уже є в каталозі", result.Message);
    }

    [Fact] // 404 на PUT/DELETE — «запис не знайдено»
    public async Task Update_404_ReturnsNotFoundText()
    {
        var api = new StubApi(_ => StubApi.Status(HttpStatusCode.NotFound));

        ApiResult result = await api.Client().UpdateAsync(ValidModel("B-gone"));

        Assert.True(result.IsNotFound);
        Assert.Equal(BooksApiClient.NotFoundText, result.Message);
    }

    [Fact] // 404 на GET — не виняток, а null
    public async Task FindAsync_404_ReturnsNull()
    {
        var api = new StubApi(_ => StubApi.Status(HttpStatusCode.NotFound));

        Assert.Null(await api.Client().FindAsync("B-404"));
    }

    [Fact] // 500 без тіла: не падаємо на розборі JSON, показуємо код
    public async Task Save_500_WithoutBody_ReturnsStatusText()
    {
        var api = new StubApi(_ => StubApi.Status(HttpStatusCode.InternalServerError));

        ApiResult result = await api.Client().CreateAsync(ValidModel());

        Assert.Equal("Сервер повернув 500.", result.Message);
    }

    [Fact] // DELETE: 204 без тіла → Ok
    public async Task DeleteAsync_204_IsOk()
    {
        var api = new StubApi(_ => StubApi.Status(HttpStatusCode.NoContent));

        ApiResult result = await api.Client().DeleteAsync("B-1");

        Assert.True(result.Ok);
        Assert.Equal((HttpMethod.Delete, "/api/books/B-1"), (api.Requests[0].Method, api.Requests[0].Url));
    }

    [Fact] // Api не запущений: зміни → ApiResult з поясненням, читання → ApiUnavailableException з тим самим текстом
    public async Task ApiDown_GivesReadableMessage()
    {
        var api = new StubApi(_ => throw new HttpRequestException("No connection could be made because the target machine actively refused it."));
        var client = api.Client();

        ApiResult save = await client.CreateAsync(ValidModel());
        var list = await Assert.ThrowsAsync<ApiUnavailableException>(() => client.ListAsync(new BookListRequest()));

        Assert.Equal(BooksApiClient.Unavailable, save.Message);
        Assert.Null(save.Status);
        Assert.Equal(BooksApiClient.Unavailable, list.Message);
        Assert.False(await client.PingAsync());
    }

    [Fact] // Api завис: спрацьовує Api:TimeoutSeconds, а не вічне «Завантаження…»
    public async Task ApiTimeout_IsReportedAsUnavailable()
    {
        var api = new StubApi(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return StubApi.Page(); });

        var error = await Assert.ThrowsAsync<ApiUnavailableException>(
            () => api.Client(TimeSpan.FromMilliseconds(100)).ListAsync(new BookListRequest()));

        Assert.Contains("не відповів вчасно", error.Message);
    }

    [Fact] // дод. завдання 4: скасування користувачем не маскується під «Api недоступний»
    public async Task CancelledByCaller_ThrowsOperationCanceled()
    {
        var api = new StubApi(async (_, ct) => { await Task.Delay(Timeout.Infinite, ct); return StubApi.Page(); });
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => api.Client().ListAsync(new BookListRequest(), cts.Token));
    }

    [Fact] // sort=title за замовчуванням не дописується — URL такий самий, як на тижні 10
    public void BookListRequest_DefaultSort_IsOmitted()
    {
        Assert.Equal("api/books?page=1&pageSize=10", new BookListRequest().ToRelativeUrl());
    }
}

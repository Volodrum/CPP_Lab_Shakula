using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Api.Contracts;
using Core.Abstractions;
using Core.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace Api.Tests;

// Дод. завдання 4: інтеграційні тести на WebApplicationFactory — справжній конвеєр ASP.NET Core
// (маршрути, фільтри, обробник винятків, JSON) у пам'яті, без порту і без мережі.
// Кожен тест — свій екземпляр застосунку з новим сховищем (SampleData), тож тести не ділять стан.
public class BooksApiTests
{
    private const string Books = "/api/books";

    private static HttpClient NewClient() => new WebApplicationFactory<Program>().CreateClient();

    private static SaveBookRequest Request(string? isbn = null, string title = "Кобзар", int copies = 2) =>
        new(isbn ?? UniqueIsbn(), title, "Тарас Шевченко", 1840, copies);

    private static string UniqueIsbn() => "979" + Random.Shared.NextInt64(0, 10_000_000_000).ToString("D10");

    [Fact] // основний сценарій дод. завдання 4: POST → 201, GET за Location → 200
    public async Task Post_Returns201WithLocation_AndGetByLocationReturns200()
    {
        using var client = NewClient();

        var post = await client.PostAsJsonAsync(Books, Request(copies: 3));

        Assert.Equal(HttpStatusCode.Created, post.StatusCode);
        var created = await post.Content.ReadFromJsonAsync<BookDto>();
        Assert.NotNull(created);
        Assert.Equal($"{Books}/{created.Id}", post.Headers.Location?.OriginalString);
        Assert.Equal((3, 3), (created.Copies, created.Available));

        var get = await client.GetAsync(post.Headers.Location);

        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        Assert.Equal(created, await get.Content.ReadFromJsonAsync<BookDto>());
    }

    [Fact] // повний цикл create-read-update-delete: після DELETE GET дає 404
    public async Task CrudCycle_AfterDelete_GetReturns404()
    {
        using var client = NewClient();
        var created = await (await client.PostAsJsonAsync(Books, Request())).Content.ReadFromJsonAsync<BookDto>();
        string url = $"{Books}/{created!.Id}";

        var put = await client.PutAsJsonAsync(url, Request(created.Isbn, "Кобзар (1840)", copies: 1));
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var updated = await put.Content.ReadFromJsonAsync<BookDto>();
        Assert.Equal(("Кобзар (1840)", 1), (updated!.Title, updated.Copies));

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.DeleteAsync(url)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PutAsJsonAsync(url, Request())).StatusCode);
    }

    [Fact] // валідація: 400 і словник errors з ключами-іменами полів
    public async Task Post_InvalidFields_Returns400WithFieldErrors()
    {
        using var client = NewClient();

        var response = await client.PostAsJsonAsync(Books, new SaveBookRequest("", "", null, 3000, -1));

        var problem = await ReadValidationProblem(response);
        Assert.Equal(["Copies", "Isbn", "Title", "Year"], problem.Errors.Keys.Order());
        Assert.Contains("ISBN обов'язковий.", problem.Errors["Isbn"]);
    }

    [Fact] // сценарій захисту: PUT з порожньою назвою — 400, видання не змінилося
    public async Task Put_EmptyTitle_Returns400_AndKeepsBook()
    {
        using var client = NewClient();

        var response = await client.PutAsJsonAsync($"{Books}/B-001", new SaveBookRequest("978-0-201-61622-4", " ", null, 1999, 4));

        Assert.Equal("Title", Assert.Single((await ReadValidationProblem(response)).Errors.Keys));
        var book = await client.GetFromJsonAsync<BookDto>($"{Books}/B-001");
        Assert.Equal("Прагматичний програміст", book!.Title);
    }

    [Theory] // тіло, що не розібралося: 400 (не 500), без слова Exception
    [InlineData("")]
    [InlineData("{not json")]
    [InlineData("[1, 2]")]
    public async Task Post_UnreadableBody_Returns400(string body)
    {
        using var client = NewClient();

        var response = await client.PostAsync(Books, new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("Exception", await response.Content.ReadAsStringAsync());
    }

    [Fact] // "copies":"abc" — 400 з помилкою саме біля поля Copies
    public async Task Post_WrongFieldType_Returns400ForThatField()
    {
        using var client = NewClient();
        const string body = """{"isbn":"9786171234567","title":"X","year":2000,"copies":"abc"}""";

        var response = await client.PostAsync(Books, new StringContent(body, Encoding.UTF8, "application/json"));

        Assert.Equal("Copies", Assert.Single((await ReadValidationProblem(response)).Errors.Keys));
    }

    [Fact]
    public async Task Post_WithoutJsonContentType_Returns415()
    {
        using var client = NewClient();

        var response = await client.PostAsync(Books, new StringContent("{}", Encoding.UTF8, "text/plain"));

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
    }

    [Fact] // DomainException → 409 через ApiExceptionHandler (дод. завдання 3)
    public async Task Post_DuplicateIsbn_Returns409ProblemDetails()
    {
        using var client = NewClient();

        var response = await client.PostAsJsonAsync(Books, Request("9780201616224")); // ISBN B-001 без дефісів

        var problem = await ReadProblem(response, HttpStatusCode.Conflict);
        Assert.Contains("B-001", problem.Detail);
    }

    [Fact] // у B-001 є видачі — вилучати не можна
    public async Task Delete_BookWithLoans_Returns409_AndBookStays()
    {
        using var client = NewClient();

        await ReadProblem(await client.DeleteAsync($"{Books}/B-001"), HttpStatusCode.Conflict);

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"{Books}/B-001")).StatusCode);
    }

    [Fact] // варіант «Бібліотека»: ?author=..., плюс пагінація { items, total }
    public async Task List_FilterByAuthor_WithPaging()
    {
        using var client = NewClient();

        var first = await client.GetFromJsonAsync<PagedResponse<BookDto>>($"{Books}?author=мартін&page=1&pageSize=2");
        var second = await client.GetFromJsonAsync<PagedResponse<BookDto>>($"{Books}?author=мартін&page=2&pageSize=2");

        Assert.Equal(3, first!.Total);
        Assert.Equal(2, first.Items.Count);
        Assert.Single(second!.Items);
        Assert.All(first.Items.Concat(second.Items), b => Assert.Contains("Мартін", b.Author));
    }

    [Fact]
    public async Task List_Default_ReturnsFirstPageOfSeed()
    {
        using var client = NewClient();

        var page = await client.GetFromJsonAsync<PagedResponse<BookDto>>(Books);

        Assert.Equal((19, 1, 20), (page!.Total, page.Page, page.PageSize));
        Assert.Equal(19, page.Items.Count);
    }

    [Theory]
    [InlineData("page=0", "Page")]
    [InlineData("pageSize=500", "PageSize")]
    public async Task List_InvalidPaging_Returns400(string query, string field)
    {
        using var client = NewClient();

        var response = await client.GetAsync($"{Books}?{query}");

        Assert.Equal(field, Assert.Single((await ReadValidationProblem(response)).Errors.Keys));
    }

    [Fact] // самоперевірка 1: OpenAPI (Development) описує всі п'ять маршрутів
    public async Task OpenApiDocument_DescribesAllFiveBookRoutes()
    {
        using var client = NewClient();

        using var doc = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var paths = doc.RootElement.GetProperty("paths");

        Assert.Equal(["get", "post"], Methods(paths.GetProperty("/api/books")));
        Assert.Equal(["delete", "get", "put"], Methods(paths.GetProperty("/api/books/{id}")));
        Assert.True(paths.GetProperty("/api/books").GetProperty("post").GetProperty("responses").TryGetProperty("409", out _));

        static string[] Methods(JsonElement path) => path.EnumerateObject().Select(p => p.Name).Order().ToArray();
    }

    [Fact] // Scalar і OpenAPI — лише в Development
    public async Task Production_HidesOpenApiAndScalar()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseEnvironment("Production"));
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/openapi/v1.json")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/scalar/v1")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(Books)).StatusCode);
    }

    [Fact] // самоперевірка 4: неочікуваний виняток — 500 без типу винятку, stack trace і шляху з диска
    public async Task UnexpectedException_Returns500_WithoutInternals()
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
            b.ConfigureTestServices(s => s.AddSingleton<IBookStore, BrokenStore>()));
        using var client = factory.CreateClient();

        var response = await client.GetAsync(Books);
        string body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("traceId", body);
        Assert.DoesNotContain("Exception", body);
        Assert.DoesNotContain(BrokenStore.SecretPath, body);
        Assert.DoesNotContain(" at ", body);
    }

    private static async Task<HttpValidationProblemDetails> ReadValidationProblem(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal(400, problem.Status);
        return problem;
    }

    private static async Task<ProblemDetails> ReadProblem(HttpResponseMessage response, HttpStatusCode expected)
    {
        Assert.Equal(expected, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.NotNull(problem);
        Assert.Equal((int)expected, problem.Status);
        return problem;
    }

    // Сховище, що «зламалося» з технічною помилкою, у тексті якої — шлях на диску.
    private sealed class BrokenStore : IBookStore
    {
        public const string SecretPath = @"C:\srv\secret\library.json";

        private static Exception Fail() => new IOException($"Disk failure while reading {SecretPath}");

        public IReadOnlyList<T> List<T>() where T : class, ILibraryEntity => throw Fail();
        public T? GetById<T>(string id) where T : class, ILibraryEntity => throw Fail();
        public void Add(ILibraryEntity item) => throw Fail();
        public void Update(ILibraryEntity item) => throw Fail();
        public bool Remove(string id) => throw Fail();
    }
}

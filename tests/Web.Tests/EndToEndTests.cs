extern alias ApiHost;

using Bunit;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Web.Components.Pages;
using Web.Contracts;
using Web.Services;
using ApiProgram = ApiHost::Program;

namespace Web.Tests;

// Наскрізно: BooksApiClient (код Web) → справжній конвеєр Api у пам'яті (маршрути, валідація, ProblemDetails)
// → CatalogService → EF Core → тимчасовий файл SQLite. Без сідування — каталог порожній, як у сценарії захисту.
public sealed class EmptyApiFactory : WebApplicationFactory<ApiProgram>
{
    private readonly string _db = Path.Combine(Path.GetTempPath(), "crossapp-tests", $"web-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:AppDb", $"Data Source={_db};Pooling=False");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Catalog:Seed", "false");
    }

    public BooksApiClient CreateBooksClient() => new(CreateClient()); // BaseAddress = http://localhost/

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        try { File.Delete(_db); } catch (IOException) { /* прибере ОС */ }
    }
}

public class EndToEndTests
{
    private static BookModel Kobzar() => new()
    {
        Isbn = "978-617-12-3456-7", Title = "Кобзар", Author = "Тарас Шевченко", Year = 1840, Copies = 2
    };

    [Fact] // сценарій захисту 2–4: порожній список → створення → редагування → невалідна форма → конфлікт → видалення
    public async Task FullScenario_AgainstRealApi()
    {
        using var factory = new EmptyApiFactory();
        var client = factory.CreateBooksClient();

        Assert.Equal(0, (await client.ListAsync(new BookListRequest())).Total);

        ApiResult created = await client.CreateAsync(Kobzar());
        Assert.True(created.Ok, created.Message);
        Assert.Equal(201, created.Status);

        var list = await client.ListAsync(new BookListRequest());
        BookDto row = Assert.Single(list.Items);
        Assert.Equal(("Кобзар", 2, 2), (row.Title, row.Copies, row.Available));

        var model = BookModel.FromDto(row);
        model.Copies = 4;
        ApiResult updated = await client.UpdateAsync(model);
        Assert.True(updated.Ok, updated.Message);
        Assert.Equal(4, (await client.FindAsync(row.Id))!.Copies);

        // Невалідна форма: порожні ISBN і назва, copies = -1 → 400 зі словником полів від FluentValidation в Api.
        ApiResult invalid = await client.CreateAsync(new BookModel { Isbn = "", Title = "", Year = 2000, Copies = -1 });
        Assert.Equal(BooksApiClient.ValidationFailed, invalid.Message);
        Assert.Equal(["Copies", "Isbn", "Title"], invalid.Errors!.Keys.Order());
        Assert.Contains("ISBN обов'язковий.", invalid.Errors["Isbn"]);

        // Правило, якого немає на клієнті (контрольний формат ISBN), — теж лише від сервера.
        ApiResult badIsbn = await client.CreateAsync(new BookModel { Isbn = "12-34", Title = "X", Year = 2000 });
        Assert.Equal("Isbn", Assert.Single(badIsbn.Errors!.Keys));

        ApiResult duplicate = await client.CreateAsync(Kobzar());
        Assert.True(duplicate.IsConflict);
        Assert.Contains("уже є в каталозі", duplicate.Message);

        Assert.True((await client.DeleteAsync(row.Id)).Ok);
        Assert.Null(await client.FindAsync(row.Id));
        Assert.True((await client.DeleteAsync(row.Id)).IsNotFound);
    }

    [Fact] // сортування і фільтр автора виконує Api, клієнт отримує вже готову сторінку
    public async Task ServerSideSortAndFilter_ThroughClient()
    {
        using var factory = new EmptyApiFactory();
        var client = factory.CreateBooksClient();
        await client.CreateAsync(Kobzar());
        await client.CreateAsync(new BookModel { Isbn = "978-966-03-4561-4", Title = "Гайдамаки", Author = "Тарас Шевченко", Year = 1841, Copies = 5 });
        await client.CreateAsync(new BookModel { Isbn = "978-966-03-4562-1", Title = "Енеїда", Author = "Іван Котляревський", Year = 1798, Copies = 1 });

        var byYearDesc = await client.ListAsync(new BookListRequest(Sort: "-year"));
        var shevchenko = await client.ListAsync(new BookListRequest(Author: "шевченко", Sort: "-copies"));

        Assert.Equal([1841, 1840, 1798], byYearDesc.Items.Select(b => b.Year));
        Assert.Equal(["Гайдамаки", "Кобзар"], shevchenko.Items.Select(b => b.Title));
    }

    [Fact] // сторінка списку над справжнім Api: «Записів ще немає.» → після POST таблиця з новим рядком
    public async Task BooksPage_OverRealApi_ShowsCreatedRow()
    {
        using var factory = new EmptyApiFactory();
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton(factory.CreateBooksClient());

        var empty = ctx.Render<Books>();
        empty.WaitForAssertion(() => Assert.Contains("Записів ще немає.", empty.Markup), TimeSpan.FromSeconds(5));

        await factory.CreateBooksClient().CreateAsync(Kobzar());
        var filled = ctx.Render<Books>();

        filled.WaitForAssertion(() => Assert.Single(filled.FindAll("tbody tr")), TimeSpan.FromSeconds(5));
        Assert.Contains("Кобзар", filled.Find("tbody tr").TextContent);
    }
}

using System.Text.Encodings.Web;
using Api;
using Api.Contracts;
using Api.Endpoints;
using Api.Errors;
using Api.Validation;
using Core;
using Core.Abstractions;
using Core.Configuration;
using Core.Services;
using Core.Storage;
using FluentValidation;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

// Тиждень 10: друга точка входу поруч із Cli. Той самий Core, інший спосіб доступу — HTTP.
// Цей файл — composition root: лише тут відомо, яке сховище стоїть за IBookStore.
var builder = WebApplication.CreateBuilder(args);

// ---------- OpenAPI-документ /openapi/v1.json (UI Scalar підключається нижче, лише в Development)
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info = new OpenApiInfo
    {
        Title = "CrossApp Library API",
        Version = "v1",
        Description = "Каталог бібліотеки (варіант Б): видання з примірниками. Помилки — ProblemDetails (RFC 9457)."
    };
    return Task.CompletedTask;
}));

// ---------- Помилки: формат RFC 9457 + власний обробник винятків (DomainException → 409, кривий JSON → 400)
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
// Помилки зв'язування (порожнє тіло, "copies":"abc", page=abc) — винятком у ApiExceptionHandler в усіх середовищах,
// щоб відповідь була однакова: 400 з текстом, а для JSON — ще й з полем, яке не розібралося.
builder.Services.Configure<RouteHandlerOptions>(o => o.ThrowOnBadRequest = true);

// JSON-контракт: camelCase (за замовчуванням для Web), кирилиця й апостроф як є, а не Ц / '.
// Relaxed-енкодер безпечний, бо відповіді йдуть як application/json і не вбудовуються в HTML-сторінку.
builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping);

// ---------- Сховище і сервіси (DI). Обробники отримують їх параметрами делегата, а не через static.
// Singleton: дані в пам'яті мусять пережити запит (Scoped дав би порожній каталог на кожен запит).
// Один екземпляр бачать паралельні запити, тому сховище загорнуто в SynchronizedBookStore (lock).
builder.Services.AddSingleton<IBookStore>(_ => new SynchronizedBookStore(CreateStore(builder.Configuration)));
// Singleton з тієї ж причини: його SemaphoreSlim робить «перевірити ISBN → додати» атомарним лише якщо він один.
builder.Services.AddSingleton<CatalogService>();
// Валідатори без стану — Singleton; ValidationFilter<T> створюється для кожної кінцевої точки і бере їх з DI.
builder.Services.AddSingleton<IValidator<SaveBookRequest>, SaveBookRequestValidator>();
builder.Services.AddSingleton<IValidator<BookListQuery>, BookListQueryValidator>();

var app = builder.Build();

app.UseExceptionHandler();  // виняток → ApiExceptionHandler → ProblemDetails, без stack trace
app.UseStatusCodePages();   // порожні 404/405 теж отримують тіло ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                 // /openapi/v1.json
    app.MapScalarApiReference();      // /scalar/v1
    app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
}

app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }))
    .WithTags("Service")
    .WithSummary("Перевірка, що сервіс живий");

app.MapBooks();

app.Run();

// Memory (за замовчуванням) або File — з appsettings.json / змінних середовища, як у Cli тижня 9.
static IBookStore CreateStore(IConfiguration configuration)
{
    var catalog = configuration.GetSection(CatalogOptions.Section).Get<CatalogOptions>() ?? new CatalogOptions();
    if (catalog.Store == CatalogStoreKind.File)
    {
        var storage = configuration.GetSection("Storage").Get<StorageOptions>() ?? new StorageOptions();
        return new FileBookStore(storage.ResolveFullPath());
    }
    return catalog.Seed ? new InMemoryBookStore(SampleData.Items()) : new InMemoryBookStore();
}

// Для WebApplicationFactory<Program> у tests/Api.Tests: клас Program з top-level statements інакше internal.
public partial class Program;

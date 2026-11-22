using System.Text.Encodings.Web;
using Api;
using Api.Contracts;
using Api.Endpoints;
using Api.Errors;
using Api.Validation;
using Core.Abstractions;
using Core.Services;
using Core.Storage;
using FluentValidation;
using Infrastructure;
using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

// Тиждень 10: друга точка входу поруч із Cli. Той самий Core, інший спосіб доступу — HTTP.
// Цей файл — composition root: лише тут відомо, яке сховище стоїть за IBookStore.
// Тиждень 11: за IBookStore тепер EfBookStore (Infrastructure, EF Core + SQLite). Маршрути, DTO і формат помилок
// не змінилися — змінилася лише реєстрація сховища в DI нижче.
// Тиждень 12: другий клієнт — Blazor (src/Web). Для нього тут лише CORS-політика і ?sort=; DTO перенесено
// в спільний src/Contracts (дод. завдання 3), JSON від цього не змінився.
var builder = WebApplication.CreateBuilder(args);

// ---------- OpenAPI-документ /openapi/v1.json (UI Scalar підключається нижче, лише в Development)
builder.Services.AddOpenApi(options => options.AddDocumentTransformer((document, _, _) =>
{
    document.Info = new OpenApiInfo
    {
        Title = "CrossApp Library API",
        Version = "v1",
        Description = "Каталог бібліотеки (варіант Б): видання з примірниками. Сховище — SQLite (EF Core). " +
                      "Помилки — ProblemDetails (RFC 9457)."
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
// DbContext — Scoped (так реєструє AddDbContext): він не потокобезпечний, тож кожен HTTP-запит має власний.
// Рядок підключення читається з ConnectionStrings:AppDb у момент створення контексту, а не тут, до Build(),
// тож його перевизначають і змінна середовища ConnectionStrings__AppDb, і тести через UseSetting.
// Відносний "data/app.db" → абсолютний шлях від ContentRootPath (SqliteConnectionStrings.Resolve).
builder.Services.AddDbContext<AppDbContext>((sp, options) => options.UseSqlite(SqliteConnectionStrings.Resolve(
    sp.GetRequiredService<IConfiguration>().GetConnectionString(SqliteConnectionStrings.Name),
    sp.GetRequiredService<IWebHostEnvironment>().ContentRootPath)));

// Сховище — Scoped, бо живе поверх Scoped DbContext. Один EfBookStore на запит обслуговує обидва контракти:
// синхронний IBookStore (тиждень 5) і асинхронний IBookStoreAsync (дод. завдання 1).
builder.Services.AddScoped<EfBookStore>();
builder.Services.AddScoped<IBookStore>(sp => sp.GetRequiredService<EfBookStore>());
// Якщо IBookStore підмінено синхронним сховищем (тест BrokenStore) — AsAsync загорне його адаптером.
builder.Services.AddScoped<IBookStoreAsync>(sp => sp.GetRequiredService<IBookStore>().AsAsync());

// CatalogService тепер теж Scoped (Singleton над Scoped-сховищем був би «полоненою залежністю»),
// а замок «одна зміна каталогу за раз» — окремий Singleton, спільний для всіх запитів.
builder.Services.AddSingleton<CatalogWriteGate>();
builder.Services.AddScoped(sp =>
    new CatalogService(sp.GetRequiredService<IBookStoreAsync>(), sp.GetRequiredService<CatalogWriteGate>()));

// Валідатори без стану — Singleton; ValidationFilter<T> створюється для кожної кінцевої точки і бере їх з DI.
builder.Services.AddSingleton<IValidator<SaveBookRequest>, SaveBookRequestValidator>();
builder.Services.AddSingleton<IValidator<BookListQuery>, BookListQueryValidator>();

// ---------- CORS (тиждень 12). Браузер блокує читання відповіді на міжджерельний fetch/XHR, якщо сервер
// не відповів Access-Control-Allow-Origin з точним origin клієнта (схема + хост + порт).
// Потрібно Blazor WebAssembly чи іншому SPA з іншого origin. Web у режимі InteractiveServer звертається до Api
// з процесу сервера (HttpClient → HttpClient), браузер у цьому запиті не бере участі, тож CORS до нього не застосовується.
// Scalar працює без CORS, бо сторінка /scalar/v1 віддається тим самим Api — це той самий origin.
// CORS — не захист Api: curl чи інший сервер ці заголовки просто ігнорують.
const string WebCorsPolicy = "web";
var webOrigins = (builder.Configuration["Cors:WebOrigin"] ?? "http://localhost:5090")
    .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
builder.Services.AddCors(o => o.AddPolicy(WebCorsPolicy, p => p
    .WithOrigins(webOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .WithExposedHeaders("Location"))); // щоб WASM-клієнт міг прочитати Location після 201

var app = builder.Build();

// ---------- Схема БД. У Development — міграції при старті (зручно на парі); у Production схему оновлюють
// командою `dotnet ef database update` або SQL-скриптом (`dotnet ef migrations script`) перед розгортанням.
// Database:MigrateOnStartup=true вмикає це й поза Development (так роблять інтеграційні тести).
if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate(); // створює app.db і застосовує всі ще не застосовані міграції (див. __EFMigrationsHistory)

    // Дод. завдання 3: порожня БД отримує стартовий каталог (Catalog:Seed, за замовчуванням true).
    var catalog = app.Configuration.GetSection(CatalogOptions.Section).Get<CatalogOptions>() ?? new CatalogOptions();
    if (catalog.Seed)
        await DbSeeder.SeedSampleDataAsync(db);
}

app.UseExceptionHandler();  // виняток → ApiExceptionHandler → ProblemDetails, без stack trace
app.UseStatusCodePages();   // порожні 404/405 теж отримують тіло ProblemDetails

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();                 // /openapi/v1.json
    app.MapScalarApiReference();      // /scalar/v1
    app.MapGet("/", () => Results.Redirect("/scalar/v1")).ExcludeFromDescription();
}

app.UseCors(WebCorsPolicy); // обов'язково до MapBooks(): політика має стояти в конвеєрі до кінцевих точок

app.MapGet("/health", () => TypedResults.Ok(new { status = "ok" }))
    .WithTags("Service")
    .WithSummary("Перевірка, що сервіс живий");

app.MapBooks();

app.Run();

// Для WebApplicationFactory<Program> у tests/Api.Tests: клас Program з top-level statements інакше internal.
public partial class Program;

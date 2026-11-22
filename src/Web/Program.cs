using Web.Components;
using Web.Services;

// Тиждень 12: Blazor Web App — другий клієнт того самого Api (перший — Cli/curl/Scalar).
// Режим Interactive Server: компоненти виконуються в цьому процесі на сервері, браузер отримує
// лише різницю DOM через SignalR (/_blazor). HttpClient нижче теж працює тут, на сервері,
// тому запит Web → Api — це виклик «сервер → сервер», до якого CORS браузера не застосовується.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Базова адреса Api — лише з конфігурації (appsettings.json → Api:BaseUrl, змінна середовища Api__BaseUrl,
// аргумент --Api:BaseUrl=...). Жодної адреси в .razor/.cs: на тижні 14 її підмінить конфігурація розгортання.
var apiBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Не задано Api:BaseUrl (appsettings.json або змінна Api__BaseUrl).");
if (!Uri.TryCreate(apiBaseUrl.TrimEnd('/') + "/", UriKind.Absolute, out var apiBaseUri))
    throw new InvalidOperationException($"Api:BaseUrl має бути абсолютною адресою, отримано «{apiBaseUrl}».");
var apiTimeout = TimeSpan.FromSeconds(builder.Configuration.GetValue("Api:TimeoutSeconds", 10));

// Типізований клієнт: IHttpClientFactory керує пулом HttpMessageHandler (немає вичерпання сокетів і застарілого DNS,
// як у new HttpClient() в кожному компоненті), а адреса й тайм-аут налаштовуються в одному місці.
builder.Services.AddHttpClient<BooksApiClient>(client =>
{
    client.BaseAddress = apiBaseUri; // зі слешем у кінці — відносні "api/books" додаються до нього
    client.Timeout = apiTimeout;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();

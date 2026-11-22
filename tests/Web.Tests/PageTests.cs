using System.Net;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Web.Components.Pages;
using Web.Services;

namespace Web.Tests;

// bUnit: сторінки рендеряться в пам'яті без браузера і без SignalR, Api підмінено StubApi.
// Перевіряються стани, які вимагає лабораторна: завантаження, порожній список, недоступний Api, 404, 400 біля полів.
public class BooksPageTests : BunitContext
{
    private StubApi Use(StubApi api)
    {
        Services.AddSingleton(api.Client());
        return api;
    }

    [Fact] // поки Api не відповів — «Завантаження…», а не NullReferenceException на першому рендері
    public void ShowsLoading_UntilApiAnswers()
    {
        var answer = new TaskCompletionSource<HttpResponseMessage>();
        Use(new StubApi((_, _) => answer.Task));

        var cut = Render<Books>();

        Assert.Contains("Завантаження…", cut.Markup);
        answer.SetResult(StubApi.Page());
        cut.WaitForAssertion(() => Assert.Contains("Записів ще немає.", cut.Markup));
    }

    [Fact]
    public void EmptyCatalog_ShowsEmptyText()
    {
        Use(new StubApi(_ => StubApi.Page()));

        var cut = Render<Books>();

        cut.WaitForAssertion(() => Assert.Equal("Записів ще немає.", cut.Find("article, p").TextContent.Trim()));
        Assert.Empty(cut.FindAll("table"));
    }

    [Fact] // Api зупинено — зрозуміле повідомлення замість порожньої таблиці
    public void ApiDown_ShowsUnavailableMessage()
    {
        Use(new StubApi(_ => throw new HttpRequestException("connection refused")));

        var cut = Render<Books>();

        cut.WaitForAssertion(() =>
            Assert.Contains(BooksApiClient.Unavailable, cut.Find(".alert-danger").TextContent));
    }

    [Fact] // рядки таблиці + підсвітка: усі примірники видано (жовтий), примірників немає (сірий)
    public void Items_RenderAsTable_WithHighlightForNoneOnShelf()
    {
        Use(new StubApi(_ => StubApi.Page(
            StubApi.Book("B-1", "Кобзар", copies: 2, available: 2),
            StubApi.Book("B-2", "Енеїда", copies: 1, available: 0),
            StubApi.Book("B-3", "Лісова пісня", copies: 0, available: 0))));

        var cut = Render<Books>();

        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("tbody tr").Count));
        var rows = cut.FindAll("tbody tr");
        Assert.True(string.IsNullOrEmpty(rows[0].ClassName));
        Assert.Contains("table-warning", rows[1].ClassName);
        Assert.Contains("table-secondary", rows[2].ClassName);
        Assert.Equal("books/B-2", rows[1].QuerySelector("a")!.GetAttribute("href"));
    }

    [Fact] // дод. завдання 4: швидкий ввід у пошук — у Api йде лише останній запит (попередній скасовано)
    public void AuthorSearch_IsDebounced_AndSentToApi()
    {
        var api = Use(new StubApi(_ => StubApi.Page(StubApi.Book())));
        var cut = Render<Books>();
        cut.WaitForAssertion(() => Assert.Single(api.Requests));

        var input = cut.Find("input[type=search]");
        input.Input("Ш");
        input.Input("Шев");
        input.Input("Шевченко");

        cut.WaitForAssertion(() => Assert.Equal(2, api.Requests.Count), TimeSpan.FromSeconds(3));
        Assert.Contains("author=Шевченко", Uri.UnescapeDataString(api.Requests[1].Url));
    }

    [Fact] // клік по заголовку «Рік» — сортування на сервері (?sort=year), повторний клік — спадання
    public void ClickOnHeader_SortsOnServer()
    {
        var api = Use(new StubApi(_ => StubApi.Page(StubApi.Book())));
        var cut = Render<Books>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tbody tr")));

        cut.FindAll("th").Single(th => th.TextContent.StartsWith("Рік")).Click();
        cut.FindAll("th").Single(th => th.TextContent.StartsWith("Рік")).Click();

        cut.WaitForAssertion(() => Assert.Equal(3, api.Requests.Count));
        Assert.Contains("sort=year", api.Requests[1].Url);
        Assert.Contains("sort=-year", api.Requests[2].Url);
    }

    [Fact] // дод. завдання 1: видалення з підтвердженням і перечитуванням списку без перезавантаження сторінки
    public void Delete_AsksConfirmation_ThenReloadsList()
    {
        bool deleted = false;
        var api = Use(new StubApi(r =>
        {
            if (r.Method == HttpMethod.Delete) { deleted = true; return StubApi.Status(HttpStatusCode.NoContent); }
            return deleted ? StubApi.Page() : StubApi.Page(StubApi.Book());
        }));
        var cut = Render<Books>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tbody tr")));

        cut.Find("tbody button.btn-outline-danger").Click();
        Assert.Contains("Видалити видання?", cut.Find(".modal").TextContent);
        Assert.False(deleted); // без підтвердження нічого не надіслано

        cut.Find(".modal .btn-danger").Click();

        cut.WaitForAssertion(() => Assert.Contains("Записів ще немає.", cut.Markup));
        Assert.Contains("вилучено", cut.Find(".alert-success").TextContent);
        Assert.Equal([HttpMethod.Get, HttpMethod.Delete, HttpMethod.Get], api.Requests.Select(r => r.Method));
    }

    [Fact] // 409 при видаленні (є видачі) — пояснення від сервера, запис лишається
    public void Delete_Conflict_ShowsServerReason()
    {
        Use(new StubApi(r => r.Method == HttpMethod.Delete
            ? StubApi.Conflict("Видання B-1 має видані примірники або історію видач.")
            : StubApi.Page(StubApi.Book())));
        var cut = Render<Books>();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tbody tr")));

        cut.Find("tbody button.btn-outline-danger").Click();
        cut.Find(".modal .btn-danger").Click();

        cut.WaitForAssertion(() => Assert.Contains("історію видач", cut.Find(".alert-warning").TextContent));
        Assert.Single(cut.FindAll("tbody tr"));
    }
}

public class BookEditPageTests : BunitContext
{
    private StubApi Use(StubApi api)
    {
        Services.AddSingleton(api.Client());
        return api;
    }

    private static void Fill(IRenderedComponent<BookEdit> cut, string isbn, string title, int year, int copies)
    {
        cut.Find("#isbn").Change(isbn);
        cut.Find("#title").Change(title);
        cut.Find("#year").Change(year.ToString());
        cut.Find("#copies").Change(copies.ToString());
    }

    [Fact] // 404 на неіснуючому id — повідомлення замість форми
    public void UnknownId_ShowsNotFound()
    {
        Use(new StubApi(_ => StubApi.Status(HttpStatusCode.NotFound)));

        var cut = Render<BookEdit>(p => p.Add(c => c.Id, "B-404"));

        cut.WaitForAssertion(() => Assert.Contains("не знайдено (404)", cut.Find(".alert-danger").TextContent));
        Assert.Empty(cut.FindAll("form"));
    }

    [Fact] // клієнтська перевірка ловить порожні поля ще до запиту
    public void ClientValidation_BlocksEmptyForm_WithoutRequest()
    {
        var api = Use(new StubApi(_ => StubApi.Json(StubApi.Book(), HttpStatusCode.Created)));
        var cut = Render<BookEdit>();

        cut.Find("form").Submit();

        Assert.Empty(api.Requests);
        var messages = cut.FindAll(".validation-message").Select(m => m.TextContent).ToList();
        Assert.Contains("ISBN обов'язковий.", messages);
        Assert.Contains("Назва обов'язкова.", messages);
    }

    [Fact] // клієнтську перевірку вимкнено — 400 від Api показується біля полів і загальним списком
    public void ServerValidationErrors_AreShownNextToFields()
    {
        var api = Use(new StubApi(_ => StubApi.ValidationProblem(new()
        {
            ["Isbn"] = ["ISBN обов'язковий."],
            ["Title"] = ["Назва обов'язкова, до 200 символів."],
            ["Copies"] = ["Кількість примірників має бути від 0 до 100."]
        })));
        var cut = Render<BookEdit>();

        cut.Find("#clientValidation").Change(false);
        Fill(cut, "", "", 2000, -1);
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("Дані не пройшли перевірку.", cut.Find(".alert").TextContent));
        Assert.Single(api.Requests);
        // Повідомлення стоїть одразу під полем Copies, а не лише в загальному списку.
        var copiesMessage = cut.Find("#copies").ParentElement!.QuerySelector(".validation-message");
        Assert.Equal("Кількість примірників має бути від 0 до 100.", copiesMessage?.TextContent);
        Assert.Equal(3, cut.FindAll(".alert li").Count);
    }

    [Fact] // виправлене поле прибирає свою серверну помилку
    public void EditingField_ClearsItsServerError()
    {
        Use(new StubApi(_ => StubApi.ValidationProblem(new() { ["Title"] = ["Назва обов'язкова, до 200 символів."] })));
        var cut = Render<BookEdit>();
        cut.Find("#clientValidation").Change(false);
        cut.Find("form").Submit();
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll(".validation-message")));

        cut.Find("#title").Change("Кобзар");

        Assert.Empty(cut.FindAll(".validation-message"));
    }

    [Fact] // 409 (дубль ISBN) — окреме загальне повідомлення, без помилок біля полів
    public void Conflict_IsShownAsGeneralMessage()
    {
        Use(new StubApi(_ => StubApi.Conflict("Видання з ISBN 978-617-12-3456-7 уже є в каталозі: B-1.")));
        var cut = Render<BookEdit>();
        Fill(cut, "978-617-12-3456-7", "Кобзар", 1840, 1);

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Contains("уже є в каталозі", cut.Find(".alert-warning").TextContent));
        Assert.Empty(cut.FindAll(".validation-message"));
    }

    [Fact] // успішне створення — POST і перехід до списку
    public void ValidForm_PostsAndNavigatesToList()
    {
        var api = Use(new StubApi(_ => StubApi.Json(StubApi.Book("B-new"), HttpStatusCode.Created)));
        var nav = Services.GetRequiredService<NavigationManager>();
        var cut = Render<BookEdit>();
        Fill(cut, "978-617-12-3456-7", "Кобзар", 1840, 2);

        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.EndsWith("/books", nav.Uri));
        Assert.Equal(HttpMethod.Post, Assert.Single(api.Requests).Method);
    }

    [Fact] // OnParametersSetAsync: той самий компонент, новий Id маршруту — дані перечитуються
    public void ChangingRouteId_ReloadsBook()
    {
        Use(new StubApi(r => StubApi.Json(r.RequestUri!.AbsolutePath.EndsWith("B-2")
            ? StubApi.Book("B-2", "Енеїда")
            : StubApi.Book("B-1", "Кобзар"))));
        var cut = Render<BookEdit>(p => p.Add(c => c.Id, "B-1"));
        cut.WaitForAssertion(() => Assert.Equal("Кобзар", cut.Find("#title").GetAttribute("value")));

        cut.Render(p => p.Add(c => c.Id, "B-2"));

        cut.WaitForAssertion(() => Assert.Equal("Енеїда", cut.Find("#title").GetAttribute("value")));
    }

    [Fact] // редагування: існуючі значення в полях, збереження — PUT
    public void EditExisting_SendsPut()
    {
        var api = Use(new StubApi(r => StubApi.Json(StubApi.Book("B-1", copies: 3, available: 1))));
        var cut = Render<BookEdit>(p => p.Add(c => c.Id, "B-1"));
        cut.WaitForAssertion(() => Assert.Contains("Зараз на полиці: 1", cut.Markup));

        cut.Find("#copies").Change("5");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => Assert.Equal(2, api.Requests.Count));
        Assert.Equal((HttpMethod.Put, "/api/books/B-1"), (api.Requests[1].Method, api.Requests[1].Url));
        Assert.Contains("\"copies\":5", api.Requests[1].Body);
    }
}

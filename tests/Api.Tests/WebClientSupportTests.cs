using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Api.Tests;

// Тиждень 12: те, що Api додав для Blazor-клієнта, — CORS-політика з origin Web і сортування ?sort= на сервері.
public class WebClientSupportTests
{
    private const string Books = "/api/books";
    private const string WebOrigin = "http://localhost:5090"; // значення Cors:WebOrigin з appsettings.json Api

    [Fact] // preflight (OPTIONS), який браузер надсилає перед POST з JSON з іншого origin (Blazor WASM)
    public async Task Preflight_FromWebOrigin_IsAllowed()
    {
        using var client = new ApiFactory().CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Options, Books);
        request.Headers.Add("Origin", WebOrigin);
        request.Headers.Add("Access-Control-Request-Method", "POST");
        request.Headers.Add("Access-Control-Request-Headers", "content-type");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(WebOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.Contains("POST", string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")));
    }

    [Fact] // чужий origin не отримує дозволу — браузер заблокує читання відповіді («Blocked by CORS policy»)
    public async Task Request_FromOtherOrigin_HasNoAllowOriginHeader()
    {
        using var client = new ApiFactory().CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Books);
        request.Headers.Add("Origin", "http://localhost:6000");

        var response = await client.SendAsync(request);

        // Сервер усе одно відповідає 200: CORS не захищає Api, а лише каже браузеру, чи можна віддати відповідь скрипту.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact] // помилка 409 теж має CORS-заголовок — інакше WASM-клієнт не прочитав би detail конфлікту
    public async Task Conflict_FromWebOrigin_StillHasCorsHeaders()
    {
        using var client = new ApiFactory().CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Delete, $"{Books}/B-001"); // у B-001 є історія видач
        request.Headers.Add("Origin", WebOrigin);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(WebOrigin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Fact] // origin задається конфігурацією (Cors:WebOrigin), а не кодом
    public async Task CorsOrigin_ComesFromConfiguration()
    {
        using var factory = new ApiFactory().WithWebHostBuilder(b => b.UseSetting("Cors:WebOrigin", "https://library.example"));
        using var client = factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, Books);
        request.Headers.Add("Origin", "https://library.example");

        var response = await client.SendAsync(request);

        Assert.Equal("https://library.example", Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
    }

    [Theory] // дод. завдання 2: сортує сервер, до пагінації
    [InlineData("year")]
    [InlineData("-year")]
    public async Task List_SortByYear_IsOrderedAcrossWholeCatalog(string sort)
    {
        using var client = new ApiFactory().CreateClient();

        var page = await client.GetFromJsonAsync<PagedResponse<BookDto>>($"{Books}?sort={sort}&pageSize=100");

        int[] years = page!.Items.Select(b => b.Year).ToArray();
        Assert.Equal(sort.StartsWith('-') ? years.OrderDescending() : years.Order(), years);
        Assert.Equal(page.Total, years.Length);
    }

    [Fact] // сортування + фільтр автора + пагінація разом
    public async Task List_SortByCopiesDescending_WithAuthorFilter()
    {
        using var client = new ApiFactory().CreateClient();

        var page = await client.GetFromJsonAsync<PagedResponse<BookDto>>($"{Books}?author=мартін&sort=-copies&pageSize=2");

        Assert.Equal(3, page!.Total);
        Assert.True(page.Items[0].Copies >= page.Items[1].Copies);
    }

    [Fact] // невідомий ключ — 400 з полем Sort, а не тихе ігнорування
    public async Task List_UnknownSort_Returns400()
    {
        using var client = new ApiFactory().CreateClient();

        var response = await client.GetAsync($"{Books}?sort=price");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>();
        Assert.Equal("Sort", Assert.Single(problem!.Errors.Keys));
    }
}

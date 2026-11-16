using System.Net;
using System.Net.Http.Json;
using Api.Contracts;

namespace Api.Tests;

// Тиждень 11: те, чого сховище в пам'яті дати не могло, — дані переживають перезапуск API,
// а дубль ISBN під паралельними запитами дає керований 409, а не 500 з «UNIQUE constraint failed».
public class PersistenceApiTests
{
    private const string Books = "/api/books";

    [Fact] // крок 8 завдання: POST → «Ctrl+C» (Dispose хоста) → новий запуск над тим самим файлом → GET
    public async Task Data_SurvivesApiRestart()
    {
        string db = ApiFactory.NewDatabasePath();
        try
        {
            BookDto created;
            using (var first = new ApiFactory(db))
            using (var client = first.CreateClient())
            {
                var post = await client.PostAsJsonAsync(Books,
                    new SaveBookRequest("978-617-7279-11-6", "Тіні забутих предків", "Михайло Коцюбинський", 1911, 2));
                Assert.Equal(HttpStatusCode.Created, post.StatusCode);
                created = (await post.Content.ReadFromJsonAsync<BookDto>())!;
            }

            using (var second = new ApiFactory(db))
            using (var client = second.CreateClient())
            {
                Assert.Equal(created, await client.GetFromJsonAsync<BookDto>($"{Books}/{created.Id}"));

                var page = await client.GetFromJsonAsync<PagedResponse<BookDto>>($"{Books}?pageSize=100");
                Assert.Equal(20, page!.Total); // 19 стартових (сідування не повторилося) + 1 новий
                Assert.Contains(page.Items, b => b.Id == created.Id);
            }
        }
        finally
        {
            ApiFactory.DeleteDatabase(db);
        }
    }

    [Fact] // самоперевірка 5: 10 одночасних POST з одним ISBN — рівно один 201, решта 409, жодного 500
    public async Task ConcurrentPosts_SameIsbn_OneCreated_Rest409()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var request = new SaveBookRequest("978-966-03-4567-8", "Лісова пісня", "Леся Українка", 1912, 1);

        var responses = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => client.PostAsJsonAsync(Books, request)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact] // DELETE видання прибирає і його примірники (каскад у БД), повторний POST того ж ISBN знову можливий
    public async Task Delete_CascadesCopies_IsbnBecomesFree()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        var request = new SaveBookRequest("978-966-500-123-4", "Захар Беркут", "Іван Франко", 1883, 3);

        var created = await (await client.PostAsJsonAsync(Books, request)).Content.ReadFromJsonAsync<BookDto>();
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync($"{Books}/{created!.Id}")).StatusCode);

        var again = await client.PostAsJsonAsync(Books, request);
        Assert.Equal(HttpStatusCode.Created, again.StatusCode);
        Assert.Equal(3, (await again.Content.ReadFromJsonAsync<BookDto>())!.Copies);
    }
}

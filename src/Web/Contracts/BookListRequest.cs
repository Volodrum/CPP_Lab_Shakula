namespace Web.Contracts;

// Параметри списку, які сторінка передає в Api як query-рядок (?author=&sort=&page=&pageSize=).
// Фільтрує, сортує й ділить на сторінки сервер (дод. завдання 2): клієнт не тягне весь каталог,
// щоб відфільтрувати його в браузері, і порядок однаковий для всіх клієнтів Api.
public sealed record BookListRequest(
    string? Author = null,
    string Sort = BookSort.Default,
    int Page = 1,
    int PageSize = 10)
{
    public string ToRelativeUrl()
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(Author))
            query.Add("author=" + Uri.EscapeDataString(Author.Trim()));
        if (!string.IsNullOrWhiteSpace(Sort) && Sort != BookSort.Default)
            query.Add("sort=" + Uri.EscapeDataString(Sort));
        query.Add($"page={Page}");
        query.Add($"pageSize={PageSize}");
        return "api/books?" + string.Join('&', query);
    }
}

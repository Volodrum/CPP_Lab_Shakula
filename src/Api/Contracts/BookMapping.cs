using Core.Services;

namespace Api.Contracts;

// Мапінг між контрактом API і Core в одному місці. Домен про DTO не знає: стрілки йдуть лише з Api в Core.
public static class BookMapping
{
    public static BookDto ToDto(this CatalogEntry entry) =>
        new(entry.Book.Id, entry.Book.Isbn, entry.Book.Title, entry.Book.Author, entry.Book.Year,
            entry.Copies, entry.OnShelf);

    public static BookDraft ToDraft(this SaveBookRequest request) =>
        new(request.Isbn, request.Title, request.Year, request.Author, request.Copies);

    // "-year" → (Year, спадання). Значення вже перевірив BookListQueryValidator.
    public static CatalogQuery ToQuery(this BookListQuery query)
    {
        string sort = string.IsNullOrWhiteSpace(query.Sort) ? BookSort.Default : query.Sort.Trim();
        bool descending = sort.StartsWith('-');
        CatalogSortField field = sort.TrimStart('-').ToLowerInvariant() switch
        {
            BookSort.Author => CatalogSortField.Author,
            BookSort.Year => CatalogSortField.Year,
            BookSort.Copies => CatalogSortField.Copies,
            _ => CatalogSortField.Title
        };
        return new CatalogQuery(query.Author, query.Search, query.Page, query.PageSize, field, descending);
    }

    public static PagedResponse<BookDto> ToResponse(this CatalogPage page) =>
        new(page.Items.Select(ToDto).ToList(), page.Total, page.Page, page.PageSize);
}

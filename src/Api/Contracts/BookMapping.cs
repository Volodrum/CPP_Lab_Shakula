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

    public static CatalogQuery ToQuery(this BookListQuery query) =>
        new(query.Author, query.Search, query.Page, query.PageSize);

    public static PagedResponse<BookDto> ToResponse(this CatalogPage page) =>
        new(page.Items.Select(ToDto).ToList(), page.Total, page.Page, page.PageSize);
}

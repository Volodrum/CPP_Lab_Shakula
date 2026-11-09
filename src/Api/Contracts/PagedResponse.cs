namespace Api.Contracts;

// Відповідь списку: { items, total, page, pageSize }. total — кількість усіх збігів фільтра,
// а не лише цієї сторінки: клієнту досить його, щоб намалювати перемикач сторінок.
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

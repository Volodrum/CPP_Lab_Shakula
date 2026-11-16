using Core.Domain;

namespace Core.Abstractions;

// Тиждень 11, дод. завдання 1: асинхронний двійник IBookStore. Синхронний контракт тижня 5 лишився без змін —
// ним користуються LendingService, Cli і звіти; HTTP-шлях (Api → CatalogService → сховище) тепер async наскрізно:
// EF Core виконує ToListAsync / SaveChangesAsync, і потік запиту не блокується на очікуванні диска.
// Core про EF нічого не знає: це лише інтерфейс, реалізація — в Infrastructure.
public interface IBookStoreAsync
{
    Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken ct = default) where T : class, ILibraryEntity;

    Task<T?> GetByIdAsync<T>(string id, CancellationToken ct = default) where T : class, ILibraryEntity;

    Task AddAsync(ILibraryEntity item, CancellationToken ct = default);

    Task UpdateAsync(ILibraryEntity item, CancellationToken ct = default);

    Task<bool> RemoveAsync(string id, CancellationToken ct = default);

    // Кілька змін як одне ціле: або всі збережено, або жодної (EF — транзакція БД).
    // Сховище, що не вміє відкотитися (пам'ять, файл), виконує work без скасування посередині: ct перевіряється
    // лише до початку, щоб скасований запит не лишив «півзміни».
    Task<TResult> InTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default);
}

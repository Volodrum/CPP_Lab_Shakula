using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

// Тиждень 11: адаптер «синхронне сховище → IBookStoreAsync». Дає CatalogService працювати над InMemoryBookStore /
// FileBookStore (тести Core, Cli) так само, як над EF-сховищем. Справжньої асинхронності тут немає — Task.FromResult.
public sealed class SyncBookStoreAdapter(IBookStore inner) : IBookStoreAsync
{
    private readonly IBookStore _inner = inner ?? throw new ArgumentNullException(nameof(inner));

    public Task<IReadOnlyList<T>> ListAsync<T>(CancellationToken ct = default) where T : class, ILibraryEntity
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_inner.List<T>());
    }

    public Task<T?> GetByIdAsync<T>(string id, CancellationToken ct = default) where T : class, ILibraryEntity
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_inner.GetById<T>(id));
    }

    public Task AddAsync(ILibraryEntity item, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _inner.Add(item);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(ILibraryEntity item, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        _inner.Update(item);
        return Task.CompletedTask;
    }

    public Task<bool> RemoveAsync(string id, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(_inner.Remove(id));
    }

    // Відкотити словник чи файл не можна, тому почату зміну доводимо до кінця: work отримує CancellationToken.None.
    public Task<TResult> InTransactionAsync<TResult>(Func<CancellationToken, Task<TResult>> work, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(work);
        ct.ThrowIfCancellationRequested();
        return work(CancellationToken.None);
    }

    public override string ToString() => $"{nameof(SyncBookStoreAdapter)} → {_inner}";
}

public static class BookStoreAsyncExtensions
{
    // Сховище, яке вже асинхронне (EfBookStore реалізує обидва інтерфейси), не загортається вдруге.
    public static IBookStoreAsync AsAsync(this IBookStore store) =>
        store as IBookStoreAsync ?? new SyncBookStoreAdapter(store);
}

namespace Core.Services;

// Тиждень 11: «одна зміна каталогу за раз» в межах процесу. На тижні 10 SemaphoreSlim жив усередині CatalogService,
// і це працювало, бо сервіс був Singleton. Тепер сервіс Scoped (над Scoped DbContext), тож замок винесено в окремий
// об'єкт, який Api реєструє як Singleton і віддає кожному екземпляру CatalogService.
// Між процесами (два запущені Api над одним app.db) порядок гарантує вже БД: унікальний індекс + транзакція.
public sealed class CatalogWriteGate
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable> EnterAsync(CancellationToken ct = default)
    {
        await _semaphore.WaitAsync(ct);
        return new Releaser(_semaphore);
    }

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;

        public void Dispose() => Interlocked.Exchange(ref _semaphore, null)?.Release();
    }
}

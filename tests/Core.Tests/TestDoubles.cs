using Core.Abstractions;
using Core.Domain;

namespace Core.Tests;

// Імпортер у пам'яті: сервіс отримує готові видання без диска (той самий ручний DI, що й у Cli).
internal sealed class FakeImporter(params Book[] books) : ICatalogImporter
{
    public Task<IReadOnlyList<Book>> LoadAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<Book>>(books);
    }
}

// «Повільне джерело»: завершується лише скасуванням токена — для тестів тайм-ауту.
internal sealed class NeverEndingImporter : ICatalogImporter
{
    public async Task<IReadOnlyList<Book>> LoadAsync(string path, CancellationToken ct = default)
    {
        await Task.Delay(Timeout.Infinite, ct);
        return [];
    }
}

// Синхронний IProgress: Progress<T> викликав би обробник у пулі потоків, і тест читав би стан раніше за нього.
internal sealed class CallbackProgress(Action<int> onReport) : IProgress<int>
{
    public void Report(int value) => onReport(value);
}

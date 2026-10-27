namespace Core.Services;

// Дод. завдання 2: зв'язані токени. Операція бачить один токен, який спрацює від будь-якого з двох джерел —
// користувача (Ctrl+C) або власного тайм-ауту. Після скасування з'ясовуємо, хто саме його спричинив:
// тайм-аут перетворюється на TimeoutException, скасування користувачем проходить наскрізь як OperationCanceledException.
public static class TimeoutPolicy
{
    public static async Task<T> RunAsync<T>(Func<CancellationToken, Task<T>> operation, TimeSpan timeout,
        CancellationToken userToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(userToken, timeoutSource.Token);
        try
        {
            return await operation(linked.Token);
        }
        catch (OperationCanceledException ex) when (timeoutSource.IsCancellationRequested && !userToken.IsCancellationRequested)
        {
            throw new TimeoutException($"Операцію перервано тайм-аутом {timeout.TotalSeconds:0.##} с.", ex);
        }
    }
}

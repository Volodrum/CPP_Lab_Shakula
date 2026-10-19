namespace Core.Reports;

// Дод. завдання 2: власний метод-розширення для топ-N, використаний у TopReaders і TopBooks.
// Другий ключ обов'язковий: без нього порядок рівних значень залежав би від порядку джерела.
public static class EnumerableExtensions
{
    public static IEnumerable<T> TopBy<T>(this IEnumerable<T> source, Func<T, int> key, int n, Func<T, string> thenBy)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(thenBy);
        ArgumentOutOfRangeException.ThrowIfNegative(n);

        // Перевірки вище — одразу під час виклику; сам ланцюжок виконується відкладено, як і стандартні оператори.
        return source.OrderByDescending(key).ThenBy(thenBy, StringComparer.Ordinal).Take(n);
    }
}

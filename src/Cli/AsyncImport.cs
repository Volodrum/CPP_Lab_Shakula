using System.Globalization;
using System.Text.Json;
using Core.Dto;

// Лабораторна 8: допоміжні типи для режиму --import-async. Сам сценарій (токен, Ctrl+C, catch) — у Program.cs.

// --import-async [шлях] [--stream] [--timeout сек] [--big N]
internal sealed record AsyncImportOptions(string Path, TimeSpan? Timeout, bool Stream, int? Big)
{
    public const int DefaultBig = 1_000_000;

    public static AsyncImportOptions Parse(string[] args)
    {
        string? ValueAfter(string key)
        {
            int i = Array.IndexOf(args, key);
            return i >= 0 && i + 1 < args.Length && !args[i + 1].StartsWith("--") ? args[i + 1] : null;
        }

        TimeSpan? timeout = double.TryParse(ValueAfter("--timeout"), NumberStyles.Float, CultureInfo.InvariantCulture,
            out double seconds) ? TimeSpan.FromSeconds(seconds) : null;
        int? big = int.TryParse(ValueAfter("--big"), out int count) ? count
            : args.Contains("--big") ? DefaultBig : null;

        return new(ValueAfter("--import-async") ?? System.IO.Path.Combine("data", "catalog.json"),
            timeout, args.Contains("--stream"), big);
    }
}

// Дод. завдання 3: підписник IProgress<int> малює відсотки в одному рядку.
// Свій клас, а не Progress<T>: той викликає обробник через пул потоків, і рядок «Імпортовано…»
// міг би надрукуватися раніше за останні відсотки. Тут Report виконується синхронно в момент виклику.
internal sealed class ConsoleProgress : IProgress<int>
{
    private bool _lineOpen;

    public void Report(int percent)
    {
        Console.Write($"\r Прогрес: {percent,3}% [{new string('#', percent / 5),-20}]");
        _lineOpen = true;
    }

    // Закрити рядок прогресу перед наступним повідомленням (успіх, скасування, помилка).
    public void EndLine()
    {
        if (_lineOpen) Console.WriteLine();
        _lineOpen = false;
    }
}

// «Великий файл» для демонстрації скасування: N синтетичних видань у тимчасовому каталозі ОС.
// Пишеться так само асинхронно (FileStream useAsync + SerializeAsync) і з тим самим токеном.
internal static class BigCatalog
{
    public static string TempPath(int count) => Path.Combine(Path.GetTempPath(), $"crossapp-catalog-{count}.json");

    public static async Task WriteAsync(string path, int count, CancellationToken ct)
    {
        await using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 1 << 16, useAsync: true);
        await JsonSerializer.SerializeAsync(stream, Generate(count), cancellationToken: ct);
    }

    // ISBN = "979" + 10 цифр номера: не перетинається з ISBN-978 із SampleData.
    private static IEnumerable<BookDto> Generate(int count)
    {
        for (int i = 0; i < count; i++)
            yield return new BookDto($"G-{i:0000000}", $"979{i:0000000000}", $"Видання {i}", 1950 + i % 76);
    }
}

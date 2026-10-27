using System.Runtime.CompilerServices;
using System.Text.Json;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;

namespace Core.Storage;

// Тиждень 8: асинхронне читання каталогу з JSON-списку BookDto (формат data/sample.json тижня 3).
// Справжній асинхронний ввід-вивід: FileStream(useAsync: true) + DeserializeAsync, без Task.Run навколо File.ReadAllText.
public sealed class JsonCatalogImporter : ICatalogImporter
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    // Увесь файл → список. Атомарно: якщо хоч один запис поганий, сервіс не отримає жодного.
    public async Task<IReadOnlyList<Book>> LoadAsync(string path, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested(); // уже скасовано — файл навіть не відкриваємо

        await using var stream = OpenRead(path);
        List<BookDto> dtos;
        try
        {
            dtos = await JsonSerializer.DeserializeAsync<List<BookDto>>(stream, Options, ct)
                ?? throw new DomainException($"Файл {Path.GetFileName(path)} не містить списку.");
        }
        catch (JsonException ex) // технічна помилка формату: загортаємо, але не губимо позицію з InnerException
        {
            throw Corrupted(path, ex);
        }

        var result = new List<Book>(dtos.Count);
        for (var i = 0; i < dtos.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            result.Add(ToBook(dtos[i], i + 1));
        }
        return result;
    }

    // Дод. завдання 1: потоковий імпорт. Елементи віддаються по одному, щойно серіалізатор їх прочитав, —
    // весь файл у пам'яті не тримається. [EnumeratorCancellation] з'єднує цей параметр з токеном,
    // переданим через WithCancellation(ct) у await foreach на боці споживача.
    public async IAsyncEnumerable<Book> StreamAsync(string path, [EnumeratorCancellation] CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        await using var stream = OpenRead(path);
        await using var items = JsonSerializer.DeserializeAsyncEnumerable<BookDto>(stream, Options, ct)
            .GetAsyncEnumerator(ct);

        for (var number = 1; ; number++)
        {
            bool hasNext;
            try
            {
                hasNext = await items.MoveNextAsync(); // yield не можна писати в try з catch, тому MoveNextAsync вручну
            }
            catch (JsonException ex)
            {
                throw Corrupted(path, ex);
            }
            if (!hasNext) yield break;

            ct.ThrowIfCancellationRequested();
            yield return ToBook(items.Current, number);
        }
    }

    private static FileStream OpenRead(string path) =>
        new(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);

    // Порушення інваріантів Book — це вже не «поганий аргумент методу», а погані дані каталогу: DomainException
    // з номером запису. ArgumentException лишається в InnerException.
    private static Book ToBook(BookDto? dto, int number)
    {
        try
        {
            return Book.FromDto(dto!); // null-елемент списку → ArgumentNullException з FromDto
        }
        catch (ArgumentException ex)
        {
            throw new DomainException($"Запис {number}: {ex.Message.Split(Environment.NewLine)[0]}", ex);
        }
    }

    private static StorageException Corrupted(string path, JsonException ex) =>
        new($"Файл {Path.GetFileName(path)} не є коректним JSON-списком видань: {ex.Message}", ex);
}

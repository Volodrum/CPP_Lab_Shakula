using System.Text.Encodings.Web;
using System.Text.Json;
using Core.Domain;
using Core.Dto;

namespace Core.Storage;

// Тиждень 9: формат файлу колекції в одному місці. Ним користуються FileBookStore (постійне сховище)
// і команди export / import у Cli, тож експортований файл можна відкрити як сховище і навпаки.
// Синхронний, як і IBookStore: другого, асинхронного контракту сховища не з'являється.
public static class LibraryJson
{
    // camelCase — звичний вигляд JSON ("isbn", "loanDays"); при читанні регістр не важить, тому файли тижнів 5–8
    // з "Isbn", "Title" теж відкриваються. UnsafeRelaxedJsonEscaping лишає кирилицю як є, без Ц:
    // файл читає людина, а в HTML він не вбудовується.
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    // Сутності → DTO → файл. Атомарно: спершу повний вміст у path.tmp, потім File.Move з перезаписом.
    // Якщо процес впаде посеред запису, зіпсується лише .tmp, а попередній path лишиться цілим.
    public static int Write(string path, IEnumerable<ILibraryEntity> items)
    {
        // Видання → примірники → видачі: у такому порядку їх і відновлюватиме Read.
        var all = items.ToList();
        List<LibraryItemDto> dtos =
        [
            .. all.OfType<Book>().Select(b => b.ToDto()),
            .. all.OfType<BookCopy>().Select(c => c.ToDto()),
            .. all.OfType<Loan>().Select(l => l.ToDto())
        ];
        if (dtos.Count != all.Count)
            throw new NotSupportedException("У файлі зберігаються лише видання, примірники й видачі.");

        string fullPath = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        string tmp = fullPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(dtos, Options));
        File.Move(tmp, fullPath, overwrite: true);
        return dtos.Count;
    }

    // Файл → DTO → сутності. FromDto повторно перевіряє інваріанти, тож рука, що поправила JSON, не обійде правила.
    // Будь-яка проблема з вмістом — StorageException з іменем файлу; IOException (немає доступу) летить як є.
    public static IReadOnlyList<ILibraryEntity> Read(string path)
    {
        string fullPath = Path.GetFullPath(path);
        List<LibraryItemDto?> dtos;
        try
        {
            dtos = JsonSerializer.Deserialize<List<LibraryItemDto?>>(File.ReadAllText(fullPath), Options)
                ?? throw new StorageException($"Файл {fullPath} містить null замість списку.", new InvalidDataException());
        }
        catch (JsonException ex)
        {
            string where = ex.LineNumber is long line ? $" (рядок {line + 1}, позиція {ex.BytePositionInLine + 1})" : "";
            throw new StorageException($"Файл {fullPath} не є коректним JSON колекції{where}: {FirstLine(ex.Message)}", ex);
        }
        catch (NotSupportedException ex) // синтаксис правильний, але елемент без "kind" — серіалізатор не знає, який record будувати
        {
            throw new StorageException(
                $"Файл {fullPath} не є файлом колекції: кожен запис має починатися з \"kind\" (book | copy | loan).", ex);
        }

        var byId = new Dictionary<string, ILibraryEntity>(StringComparer.OrdinalIgnoreCase);
        try
        {
            // Порядок важливий: видача посилається на примірник, тому спершу відновлюємо примірники.
            foreach (var dto in dtos.OfType<BookDto>()) Put(Book.FromDto(dto));
            foreach (var dto in dtos.OfType<BookCopyDto>()) Put(BookCopy.FromDto(dto));
            foreach (var dto in dtos.OfType<LoanDto>())
            {
                var copy = byId.GetValueOrDefault(dto.CopyId) as BookCopy
                    ?? throw new InvalidDataException($"видача {dto.Id} посилається на відсутній примірник {dto.CopyId}");
                Put(Loan.FromDto(dto, copy));
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException)
        {
            throw new StorageException($"Файл {fullPath} містить некоректний запис: {FirstLine(ex.Message)}", ex);
        }
        return [.. byId.Values];

        void Put(ILibraryEntity item)
        {
            if (!byId.TryAdd(item.Id, item))
                throw new InvalidDataException($"id={item.Id} трапляється у файлі двічі");
        }
    }

    private static string FirstLine(string message) => message.Split(Environment.NewLine)[0];
}

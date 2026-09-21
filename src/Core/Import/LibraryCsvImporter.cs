using System.Globalization;
using System.Text;
using Core.Dto;

namespace Core.Import;

public static class LibraryCsvImporter
{
    private const char Separator = ';';

    public static ImportResult<LibraryItemDto> Load(string path)
    {
        var items = new List<LibraryItemDto>();
        var errors = new List<string>();

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);

        for (int i = 0; i < lines.Length; i++)
        {
            int number = i + 1;
            string line = lines[i];

            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith('#'))
                continue;

            if (number == 1 && (line.StartsWith("type", StringComparison.OrdinalIgnoreCase) || line.StartsWith("id", StringComparison.OrdinalIgnoreCase)))
                continue;

            switch (ParseLine(line))
            {
                case ParseOk ok:
                    items.Add(ok.Value);
                    break;
                case ParseFailed failed:
                    errors.Add($"рядок {number}: {failed.Reason}");
                    break;
            }
        }

        return new ImportResult<LibraryItemDto>(items, errors);
    }

    private static ParseOutcome ParseLine(string line)
    {
        string[] parts = line.Split(Separator, StringSplitOptions.TrimEntries);
        int currentYear = DateTime.Now.Year;

        return parts switch
        {
            // --- Невідомий префікс ---
            [var prefix, ..] when prefix != "B" && prefix != "b" && prefix != "R" && prefix != "r"
                => new ParseFailed($"невідомий тип запису '{prefix}' (очікується 'B' або 'R')"),

            // --- Книги (префікс 'B') ---
            ["B" or "b", ..] when parts.Length < 5
                => new ParseFailed($"очікую щонайменше 5 колонок для книги, отримав {parts.Length}"),
            ["B" or "b", "", ..] => new ParseFailed("ID книги порожній"),
            ["B" or "b", _, "", _, ..] or ["B" or "b", _, _, "", ..] => new ParseFailed("ISBN або назва книги порожні"),
            ["B" or "b", _, _, _, var yearStr, ..] when !int.TryParse(yearStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) || y < 1450 || y > currentYear
                => new ParseFailed($"рік книги '{yearStr}' не є числом або виходить за допустимі межі (1450..{currentYear})"),
            ["B" or "b", var id, var isbn, var title, var yearStr]
                => new ParseOk(new BookDto(id, isbn, title, int.Parse(yearStr, CultureInfo.InvariantCulture))),
            ["B" or "b", var id, var isbn, var title, var yearStr, var author]
                => new ParseOk(new BookDto(id, isbn, title, int.Parse(yearStr, CultureInfo.InvariantCulture), string.IsNullOrWhiteSpace(author) ? null : author)),

            // --- Читачі (префікс 'R') ---
            ["R" or "r", ..] when parts.Length < 4
                => new ParseFailed($"очікую щонайменше 4 колонки для читача, отримав {parts.Length}"),
            ["R" or "r", "", ..] => new ParseFailed("ID читача порожній"),
            ["R" or "r", _, "", _, ..] or ["R" or "r", _, _, "", ..] => new ParseFailed("ПІБ або номер читацького квитка порожні"),
            ["R" or "r", var id, var name, var ticket]
                => new ParseOk(new ReaderDto(id, name, ticket)),
            ["R" or "r", var id, var name, var ticket, var phone]
                => new ParseOk(new ReaderDto(id, name, ticket, string.IsNullOrWhiteSpace(phone) ? null : phone)),

            _ => new ParseFailed($"занадто багато колонок: {parts.Length}")
        };
    }

    private abstract record ParseOutcome;
    private sealed record ParseOk(LibraryItemDto Value) : ParseOutcome;
    private sealed record ParseFailed(string Reason) : ParseOutcome;
}

using System.Text;
using System.Text.Json;
using Core.Dto;

namespace Core.Import;

public static class BookJsonImporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static ImportResult<BookDto> Load(string path)
    {
        var items = new List<BookDto>();
        var errors = new List<string>();

        try
        {
            string json = File.ReadAllText(path, Encoding.UTF8);
            List<BookDto> list = JsonSerializer.Deserialize<List<BookDto>>(json, JsonOptions) ?? [];

            for (int i = 0; i < list.Count; i++)
            {
                int index = i + 1;
                BookDto? book = list[i];

                if (book is null)
                {
                    errors.Add($"елемент {index}: об'єкт порожній (null)");
                    continue;
                }

                int currentYear = DateTime.Now.Year;

                switch (book)
                {
                    case { Id: "" or { Length: 0 } }:
                        errors.Add($"елемент {index}: ID порожній");
                        break;
                    case { Isbn: "" or { Length: 0 } } or { Title: "" or { Length: 0 } }:
                        errors.Add($"елемент {index} ({book.Id}): ISBN або назва порожні");
                        break;
                    case { Year: var y } when y < 1450 || y > currentYear:
                        errors.Add($"елемент {index} ({book.Id}): рік '{book.Year}' поза допустимими межами (1450..{currentYear})");
                        break;
                    default:
                        items.Add(book);
                        break;
                }
            }
        }
        catch (JsonException ex)
        {
            errors.Add($"Помилка структури JSON: {ex.Message}");
        }
        catch (Exception ex)
        {
            errors.Add($"Помилка читання файлу: {ex.Message}");
        }

        return new ImportResult<BookDto>(items, errors);
    }
}

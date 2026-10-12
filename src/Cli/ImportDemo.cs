using Core.Dto;
using Core.Import;

// Лабораторна 3: імпорт CSV/JSON (запуск: dotnet run --project src/Cli -- --import [шлях]
// або dotnet run --project src/Cli -- data/mixed.csv).
// Лабораторна 6, дод. завдання 2: про пропущені рядки імпортер повідомляє через Action<string> під час читання.
internal static class ImportDemo
{
    public static int Run(string path)
    {
        if (!File.Exists(path))
        {
            Console.WriteLine($"Файл не знайдено: {Path.GetFullPath(path)}");
            return 1;
        }

        string extension = Path.GetExtension(path).ToLowerInvariant();
        string fileName = Path.GetFileName(path).ToLowerInvariant();

        int skipped = 0;
        Action<string> reportSkipped = reason => Console.WriteLine($" ! пропущено #{++skipped}: {reason}");

        return extension switch
        {
            ".json" => ProcessResult(BookJsonImporter.Load(path, reportSkipped), FormatBook),
            ".csv" when fileName.Contains("mixed") => ProcessResult(LibraryCsvImporter.Load(path, reportSkipped), FormatLibraryItem),
            ".csv" => ProcessResult(BookCsvImporter.Load(path, reportSkipped), FormatBook),
            _ => HandleUnsupported(extension)
        };
    }

    // Пропущені рядки вже надруковано колбеком під час імпорту, тут — лише прийняті записи й підсумок.
    private static int ProcessResult<T>(ImportResult<T> result, Func<T, string> formatter)
    {
        Console.WriteLine($"Завантажено записів: {result.Items.Count}");

        foreach (T item in result.Items.Take(5))
        {
            Console.WriteLine(formatter(item));
        }

        if (result.Errors.Count > 0)
            Console.WriteLine($"Пропущено рядків: {result.Errors.Count}");

        Console.WriteLine($"Статистика імпорту: {result.Summary}");
        return 0;
    }

    private static string FormatBook(BookDto b) =>
        $" {b.Id,-6} {b.Isbn,-18} {b.Title,-30} {b.Year,4}  {b.Author}";

    private static string FormatLibraryItem(LibraryItemDto item) => item switch
    {
        BookDto b => $" [Книга] {b.Id,-6} {b.Isbn,-18} {b.Title,-26} {b.Year,4}  {b.Author}",
        ReaderDto r => $" [Читач] {r.Id,-6} {r.TicketNumber,-18} {r.FullName,-26} {r.Phone}",
        _ => $" {item.Id}: {item}"
    };

    private static int HandleUnsupported(string ext)
    {
        Console.WriteLine($"Непідтримуваний формат файлу '{ext}'. Очікується .csv або .json.");
        return 1;
    }
}

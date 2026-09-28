using System.Text;
using Core;
using Core.Dto;
using Core.Import;

Console.OutputEncoding = Encoding.UTF8;

if (args.Length > 0 && (args[0] == "--info" || args[0] == "--env"))
{
    EnvironmentReport report = EnvironmentInfo.Collect();
    Console.WriteLine("CrossApp – Інформація про середовище");
    Console.WriteLine($"Студент: Шакула Володимир, група ФЕІ-32с");
    Console.WriteLine(new string('-', 56));
    Console.WriteLine($"ОС (OSDescription)  : {report.OsDescription}");
    Console.WriteLine($"Runtime             : {report.FrameworkDescription}");
    Console.WriteLine($"Архітектура процесу : {report.ProcessArchitecture}");
    Console.WriteLine($"RID (визначено)     : {report.DetectedRid}");
    Console.WriteLine($"RID (від .NET)      : {report.ReportedRid}");
    Console.WriteLine($"Каталог застосунку  : {report.BaseDirectory}");
    Console.WriteLine($"Цільова збірка Core : {report.BuildTargetNote}");
    return 0;
}

if (args.Length > 0 && args[0] == "--domain")
{
    return DomainDemo.Run(args.Length > 1 ? args[1] : Path.Combine("data", "books_domain.csv"));
}

string path = args.Length > 0 ? args[0] : Path.Combine("data", "sample.csv");

if (!File.Exists(path))
{
    Console.WriteLine($"Файл не знайдено: {Path.GetFullPath(path)}");
    return 1;
}

string extension = Path.GetExtension(path).ToLowerInvariant();
string fileName = Path.GetFileName(path).ToLowerInvariant();

return extension switch
{
    ".json" => ProcessResult(BookJsonImporter.Load(path), FormatBook),
    ".csv" when fileName.Contains("mixed") => ProcessResult(LibraryCsvImporter.Load(path), FormatLibraryItem),
    ".csv" => ProcessResult(BookCsvImporter.Load(path), FormatBook),
    _ => HandleUnsupported(extension)
};

static int ProcessResult<T>(ImportResult<T> result, Func<T, string> formatter)
{
    Console.WriteLine($"Завантажено записів: {result.Items.Count}");

    foreach (T item in result.Items.Take(5))
    {
        Console.WriteLine(formatter(item));
    }

    if (result.Errors.Count > 0)
    {
        Console.WriteLine($"Пропущено рядків: {result.Errors.Count}");
        foreach (string error in result.Errors)
        {
            Console.WriteLine($" ! {error}");
        }
    }

    Console.WriteLine($"Статистика імпорту: {result.Summary}");
    return 0;
}

static string FormatBook(BookDto b) =>
    $" {b.Id,-6} {b.Isbn,-18} {b.Title,-30} {b.Year,4}  {b.Author}";

static string FormatLibraryItem(LibraryItemDto item) => item switch
{
    BookDto b => $" [Книга] {b.Id,-6} {b.Isbn,-18} {b.Title,-26} {b.Year,4}  {b.Author}",
    ReaderDto r => $" [Читач] {r.Id,-6} {r.TicketNumber,-18} {r.FullName,-26} {r.Phone}",
    _ => $" {item.Id}: {item}"
};

static int HandleUnsupported(string ext)
{
    Console.WriteLine($"Непідтримуваний формат файлу '{ext}'. Очікується .csv або .json.");
    return 1;
}
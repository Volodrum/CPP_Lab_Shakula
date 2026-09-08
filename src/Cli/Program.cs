using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Core;

Console.OutputEncoding = Encoding.UTF8;

EnvironmentReport report = EnvironmentInfo.Collect();

const string student = "Шакула Володимир, група ФЕІ-32с";
const string domain = "Бібліотека (видання, примірники, читачі, видачі та повернення)";

if (args.Contains("--json"))
{
    var jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
    };

    var outputData = new
    {
        Title = "CrossApp – Інформація про середовище",
        Student = student,
        Domain = domain,
        Environment = report
    };

    Console.WriteLine(JsonSerializer.Serialize(outputData, jsonOptions));
}
else
{
    Console.WriteLine("CrossApp – Інформація про середовище");
    Console.WriteLine($"Студент: {student}");
    Console.WriteLine(new string('-', 56));
    Console.WriteLine($"ОС (OSDescription)  : {report.OsDescription}");
    Console.WriteLine($"Runtime             : {report.FrameworkDescription}");
    Console.WriteLine($"Архітектура процесу : {report.ProcessArchitecture}");
    Console.WriteLine($"RID (визначено)     : {report.DetectedRid}");
    Console.WriteLine($"RID (від .NET)      : {report.ReportedRid}");
    Console.WriteLine($"Каталог застосунку  : {report.BaseDirectory}");
    Console.WriteLine($"Цільова збірка Core : {report.BuildTargetNote}");
    Console.WriteLine(new string('-', 56));
    Console.WriteLine($"Предметна область   : {domain}");
}
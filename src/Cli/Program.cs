using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;


Console.OutputEncoding = Encoding.UTF8;

var domain = "Бібліотека (видання, примірники, читачі, видачі та повернення)";
var student = "Шакула Володимир, група ___";

var info = new
{
    Title = "CrossApp – практикум з крос-платформного програмування",
    Student = student,
    OsDescription = RuntimeInformation.OSDescription,
    OsVersion = Environment.OSVersion.ToString(),
    ProcessArchitecture = RuntimeInformation.ProcessArchitecture.ToString(),
    DotNetVersion = Environment.Version.ToString(),
    FrameworkDescription = RuntimeInformation.FrameworkDescription,
    AppBaseDirectory = AppContext.BaseDirectory,
    CurrentDirectory = Environment.CurrentDirectory,
    Domain = domain
};

if (args.Contains("--json"))
{
    var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
    Console.WriteLine(JsonSerializer.Serialize(info, jsonOptions));
}
else
{
    Console.WriteLine(info.Title);
    Console.WriteLine($"Студент: {info.Student}");
    Console.WriteLine(new string('-', 60));
    Console.WriteLine($"ОС (OSDescription)     : {info.OsDescription}");
    Console.WriteLine($"ОС (Environment)       : {info.OsVersion}");
    Console.WriteLine($"Архітектура процесу    : {info.ProcessArchitecture}");
    Console.WriteLine($"Версія .NET (CLR)      : {info.DotNetVersion}");
    Console.WriteLine($"Runtime                : {info.FrameworkDescription}");
    Console.WriteLine($"Каталог застосунку     : {info.AppBaseDirectory}");
    Console.WriteLine($"Поточний каталог       : {info.CurrentDirectory}");
    Console.WriteLine(new string('-', 60));
    Console.WriteLine($"Предметна область      : {info.Domain}");
}
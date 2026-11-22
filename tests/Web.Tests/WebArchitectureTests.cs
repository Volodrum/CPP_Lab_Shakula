using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Web.Services;

namespace Web.Tests;

// Самоперевірка 1–2 лабораторної автоматично: Web знає лише HTTP-контракт.
public class WebArchitectureTests
{
    private static readonly string Root = FindRoot();
    private static string WebDir => Path.Combine(Root, "src", "Web");

    private static string FindRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CrossApp.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Не знайдено CrossApp.sln");
    }

    [Fact] // Web посилається лише на спільні DTO, без Core і Infrastructure
    public void WebProject_ReferencesOnlyContracts()
    {
        var csproj = XDocument.Load(Path.Combine(WebDir, "Web.csproj"));

        var references = csproj.Descendants("ProjectReference")
            .Select(r => Path.GetFileNameWithoutExtension(r.Attribute("Include")!.Value)).ToList();
        var packages = csproj.Descendants("PackageReference").Select(p => p.Attribute("Include")!.Value).ToList();

        Assert.Equal(["Contracts"], references);
        Assert.DoesNotContain(packages, p => p.Contains("EntityFramework") || p.Contains("Sqlite"));
    }

    [Fact] // і в скомпільованій збірці теж: жодного Core, Infrastructure, EF Core
    public void WebAssembly_DoesNotLoadDataAccess()
    {
        var names = typeof(BooksApiClient).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.Contains("Contracts", names);
        Assert.DoesNotContain(names, n => n is "Core" or "Infrastructure" || n.StartsWith("Microsoft.EntityFrameworkCore")
                                          || n.StartsWith("Microsoft.Data.Sqlite"));
    }

    [Fact] // самоперевірка 1: у .razor і .cs Web немає жодного localhost-літерала чи порту Api
    public void WebSources_HaveNoHardcodedApiAddress()
    {
        var offenders = Directory.EnumerateFiles(WebDir, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".cs") || f.EndsWith(".razor"))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(f => Regex.IsMatch(File.ReadAllText(f), @"localhost|127\.0\.0\.1|:5080"))
            .Select(Path.GetFileName)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact] // адреса Api — у конфігурації; рядка підключення до БД у Web немає
    public void WebAppsettings_HasApiBaseUrl_AndNoConnectionString()
    {
        using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(WebDir, "appsettings.json")));
        var root = json.RootElement;

        Assert.True(Uri.IsWellFormedUriString(root.GetProperty("Api").GetProperty("BaseUrl").GetString(), UriKind.Absolute));
        Assert.False(root.TryGetProperty("ConnectionStrings", out _));
    }

    [Fact] // origin Web у CORS Api збігається з портом Web (5090) з launchSettings
    public void ApiCorsOrigin_MatchesWebLaunchUrl()
    {
        using var api = JsonDocument.Parse(File.ReadAllText(Path.Combine(Root, "src", "Api", "appsettings.json")));
        using var launch = JsonDocument.Parse(File.ReadAllText(Path.Combine(WebDir, "Properties", "launchSettings.json")));

        string origin = api.RootElement.GetProperty("Cors").GetProperty("WebOrigin").GetString()!;
        string webUrl = launch.RootElement.GetProperty("profiles").GetProperty("http").GetProperty("applicationUrl").GetString()!;

        Assert.Equal(new Uri(webUrl).GetLeftPart(UriPartial.Authority), origin);
    }

    [Fact] // спільний проєкт Contracts нічого не тягне за собою (дод. завдання 3)
    public void ContractsAssembly_HasNoProjectDependencies()
    {
        var names = typeof(BookDto).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.All(names, n => Assert.True(n.StartsWith("System") || n == "netstandard", n));
    }
}

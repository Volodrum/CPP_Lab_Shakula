using System.Reflection;
using Core.Configuration;
using Microsoft.Extensions.Configuration;

// Лабораторна 9: збирання конфігурації. Пакети Microsoft.Extensions.Configuration.* підключено лише до Cli —
// Core отримує готовий AppOptions і не знає, звідки взялося значення.
internal sealed record AppSettings(
    string EnvironmentName,
    AppOptions Options,
    StorageOptions Storage,
    string DataFile,
    string? ApiKey,
    bool DataPathFromLegacyVariable,
    ConsoleLog Log)
{
    public const string EnvPrefix = "CROSSAPP_";
    public const string LegacyDataPathVariable = "CROSSAPP_DATA_PATH";

    // Джерела в порядку додавання: кожне наступне перекриває однакові ключі попередніх.
    //   appsettings.json → appsettings.{Environment}.json → user-secrets (лише Development)
    //   → змінні CROSSAPP_* → аргументи --Ключ=значення → окремо CROSSAPP_DATA_PATH поверх Storage:DataPath.
    public static AppSettings Load(string[] args)
    {
        string envName = Environment.GetEnvironmentVariable("CROSSAPP_ENVIRONMENT") is { Length: > 0 } fromEnv
            ? fromEnv
            : "Development";

        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory) // не cwd: файли лежать поруч зі збіркою, звідки б її не запустили
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{envName}.json", optional: true, reloadOnChange: false);

        // Як в ASP.NET Core: secrets.json — інструмент розробника, у Production значення приходять зі змінних середовища.
        if (envName == "Development")
            builder.AddUserSecrets(Assembly.GetExecutingAssembly(), optional: true);

        IConfiguration config = builder
            .AddEnvironmentVariables(prefix: EnvPrefix) // CROSSAPP_Storage__DataPath → Storage:DataPath
            .AddCommandLine(ConfigArguments(args))
            .Build();

        AppOptions options = config.Get<AppOptions>() ?? new AppOptions();

        // Проста змінна без вкладеності — для скриптів і CI, де зручніше одне ім'я, ніж Storage__DataPath.
        string? legacy = Environment.GetEnvironmentVariable(LegacyDataPathVariable);
        bool fromLegacy = !string.IsNullOrWhiteSpace(legacy);
        var storage = new StorageOptions
        {
            Root = options.Storage.Root,
            DataPath = fromLegacy ? legacy! : options.Storage.DataPath,
            FileName = options.Storage.FileName
        };

        var log = new ConsoleLog(options.Logging.MinimumLevel);
        log.Debug($"Середовище: {envName} (CROSSAPP_ENVIRONMENT {(Environment.GetEnvironmentVariable("CROSSAPP_ENVIRONMENT") is null ? "не задано" : "задано")})");
        log.Debug($"Робочий каталог (Environment.CurrentDirectory): {Environment.CurrentDirectory}");
        log.Debug($"Каталог застосунку (AppContext.BaseDirectory): {AppContext.BaseDirectory}");
        log.Debug($"Джерела: {string.Join(" → ", ((IConfigurationRoot)config).Providers.Select(Describe))}");

        return new AppSettings(envName, options, storage, storage.ResolveFullPath(), config["Integration:ApiKey"],
            fromLegacy, log);
    }

    // Вхідні файли попередніх лаб копіюються в bin/…/samples (Cli.csproj), тож шлях не залежить від cwd.
    public static string Sample(string fileName) => Path.Combine(AppContext.BaseDirectory, "samples", fileName);

    // Секрет не друкується: лише факт наявності та довжина.
    public string ApiKeyStatus => ApiKey is null ? "не заданий" : $"присутній, довжина {ApiKey.Length}";

    // AddCommandLine бере лише --Ключ=значення: решта аргументів (export, --file, report-top) — команди програми,
    // і провайдер не повинен приймати «--file --cache» за ключ file зі значенням --cache.
    private static string[] ConfigArguments(string[] args) =>
        [.. args.Where(a => a.StartsWith("--") && a.Contains('='))];

    private static string Describe(IConfigurationProvider provider) => provider switch
    {
        FileConfigurationProvider file => file.Source.Path ?? "json",
        _ => provider.GetType().Name.Replace("ConfigurationProvider", "")
    };
}

// Найпростіший журнал із рівнем з Logging:MinimumLevel: Development бачить [debug], Production — лише попередження.
internal sealed class ConsoleLog(LogLevel minimum)
{
    public LogLevel Minimum => minimum;

    public void Debug(string message) => Write(LogLevel.Debug, message);
    public void Info(string message) => Write(LogLevel.Information, message);
    public void Warn(string message) => Write(LogLevel.Warning, message);

    private void Write(LogLevel level, string message)
    {
        if (level < minimum) return;
        var color = Console.ForegroundColor;
        Console.ForegroundColor = level switch
        {
            LogLevel.Debug => ConsoleColor.DarkGray,
            LogLevel.Warning or LogLevel.Error => ConsoleColor.Yellow,
            _ => color
        };
        Console.WriteLine($"[{level.ToString().ToLowerInvariant()}] {message}");
        Console.ForegroundColor = color;
    }
}

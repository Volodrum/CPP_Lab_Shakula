namespace Core.Configuration;

// Тиждень 9: налаштування застосунку. Звідки вони прийшли (appsettings.json, змінні середовища, аргументи) —
// вирішує Cli через ConfigurationBuilder; Core бачить лише готовий об'єкт і не знає ні про JSON, ні про user-secrets.
// Значення за замовчуванням — ті, з якими програма працювала до тижня 9, тож порожня конфігурація нічого не ламає.
public sealed class AppOptions
{
    public string EnvironmentName { get; init; } = "Production";
    public StorageOptions Storage { get; init; } = new();
    public LoggingOptions Logging { get; init; } = new();
    public LibraryOptions Library { get; init; } = new();
}

// Відносно чого рахувати відносний DataPath.
public enum StorageRoot
{
    App,           // AppContext.BaseDirectory — поруч зі збіркою (bin/…/net10.0)
    LocalAppData   // Environment.SpecialFolder.LocalApplicationData — дані користувача, що переживають перевстановлення
}

public sealed class StorageOptions
{
    public StorageRoot Root { get; init; } = StorageRoot.App;
    public string DataPath { get; init; } = "data";
    public string FileName { get; init; } = "library.json";

    // Базовий каталог для Root: явний, а не Environment.CurrentDirectory, що залежить від того, звідки запустили процес.
    public string BaseDirectory() => Root switch
    {
        StorageRoot.LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData,
            Environment.SpecialFolderOption.Create),
        _ => AppContext.BaseDirectory
    };

    // Абсолютний DataPath (C:\…, /tmp/…) використовується як є, відносний — від baseDir.
    // Каталог створюється тут, тож видалений data/ просто з'явиться знову.
    public string ResolveFullPath(string baseDir)
    {
        if (string.IsNullOrWhiteSpace(FileName) || Path.GetFileName(FileName) != FileName)
            throw new ArgumentException($"Storage:FileName має бути іменем файлу без каталогів, а не '{FileName}'.");

        var root = Path.IsPathRooted(DataPath) ? DataPath : Path.Combine(baseDir, DataPath);
        Directory.CreateDirectory(root);
        return Path.GetFullPath(Path.Combine(root, FileName));
    }

    public string ResolveFullPath() => ResolveFullPath(BaseDirectory());
}

public enum LogLevel
{
    Debug,
    Information,
    Warning,
    Error
}

public sealed class LoggingOptions
{
    public LogLevel MinimumLevel { get; init; } = LogLevel.Information;
}

// Ключ варіанта «Бібліотека»: строк, на який видається примірник (Library:LoanDays).
public sealed class LibraryOptions
{
    public int LoanDays { get; init; } = Domain.Loan.DefaultLoanDays;
}

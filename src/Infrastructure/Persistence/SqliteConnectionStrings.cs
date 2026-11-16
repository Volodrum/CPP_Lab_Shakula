using Microsoft.Data.Sqlite;

namespace Infrastructure.Persistence;

// Рядок підключення з конфігурації (ConnectionStrings:AppDb) → готовий до UseSqlite.
// Відносний Data Source рахується від ContentRootPath застосунку, а не від поточного каталогу процесу:
// інакше `dotnet run` з кореня репозиторію і `dotnet ef database update` писали б у два різні app.db
// (типова помилка «no such table: Books»). Шлях збирається Path.Combine — однаково у Windows, Linux і macOS.
public static class SqliteConnectionStrings
{
    public const string Name = "AppDb";
    public const string Default = "Data Source=data/app.db";

    public static string Resolve(string? raw, string contentRootPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);
        var csb = new SqliteConnectionStringBuilder(string.IsNullOrWhiteSpace(raw) ? Default : raw);

        // ":memory:" і "file:…?mode=memory" — не файли, шлях до них не будуємо.
        bool inMemory = csb.Mode == SqliteOpenMode.Memory
                        || csb.DataSource == ":memory:"
                        || csb.DataSource.Contains("mode=memory", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(csb.DataSource))
            throw new ArgumentException($"ConnectionStrings:{Name} не містить Data Source.", nameof(raw));

        if (!inMemory)
        {
            // "data/app.db" у конфігурації → роздільник поточної ОС, абсолютний шлях від ContentRoot.
            string relative = csb.DataSource.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            string full = Path.GetFullPath(Path.IsPathRooted(relative) ? relative : Path.Combine(contentRootPath, relative));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!); // видалений data/ з'явиться знову
            csb.DataSource = full;
        }

        return csb.ConnectionString;
    }
}

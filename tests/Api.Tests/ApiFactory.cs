using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;

namespace Api.Tests;

// Тиждень 11: кожен екземпляр API в тестах — над власним тимчасовим файлом SQLite. Міграції й стартові дані
// застосовує сам Program.cs (Database:MigrateOnStartup=true — і в Production-тесті теж), тож тести перевіряють
// справжній ланцюжок HTTP → CatalogService → EfBookStore → SQLite, а не підмінене сховище.
// data/app.db розробника тести не чіпають.
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly bool _ownsDatabase;

    // databasePath == null — новий файл, який фабрика видалить під час Dispose.
    // Переданий шлях — спільний для кількох «запусків» API (тест перезапуску), видаляє його сам тест.
    public ApiFactory(string? databasePath = null)
    {
        _ownsDatabase = databasePath is null;
        DatabasePath = databasePath ?? NewDatabasePath();
    }

    public string DatabasePath { get; }

    public static string NewDatabasePath() =>
        Path.Combine(Path.GetTempPath(), "crossapp-tests", $"api-{Guid.NewGuid():N}.db");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Pooling=False: після Dispose файл одразу звільняється і його можна видалити.
        builder.UseSetting("ConnectionStrings:AppDb", $"Data Source={DatabasePath};Pooling=False");
        builder.UseSetting("Database:MigrateOnStartup", "true");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && _ownsDatabase)
            DeleteDatabase(DatabasePath);
    }

    public static void DeleteDatabase(string path)
    {
        SqliteConnection.ClearAllPools();
        foreach (string file in new[] { path, path + "-wal", path + "-shm" })
        {
            try { File.Delete(file); }
            catch (IOException) { /* файл ще тримає інший процес — тимчасовий каталог прибере ОС */ }
        }
    }
}

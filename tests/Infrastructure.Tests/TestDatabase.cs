using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Tests;

// Окремий тимчасовий файл SQLite на тест + міграції (не EnsureCreated): тести бачать ту саму схему, що й Api.
// NewContext() — «новий запуск програми»: свіжий DbContext без кешу трекера, дані читаються лише з файлу.
public sealed class TestDatabase : IDisposable
{
    public string Path { get; } =
        System.IO.Path.Combine(System.IO.Path.GetTempPath(), "crossapp-tests", $"infra-{Guid.NewGuid():N}.db");

    public string ConnectionString => $"Data Source={Path};Pooling=False";

    public TestDatabase(bool migrate = true)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        if (migrate)
        {
            using var db = NewContext();
            db.Database.Migrate();
        }
    }

    public AppDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(ConnectionString).Options);

    public EfBookStore NewStore(AppDbContext db) => new(db);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (string file in new[] { Path, Path + "-wal", Path + "-shm" })
        {
            try { File.Delete(file); }
            catch (IOException) { }
        }
    }
}

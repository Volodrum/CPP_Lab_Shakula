using System.Reflection;
using Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Infrastructure.Tests;

// Схема з міграцій: таблиці, ключі, 1:N, порядок міграцій, відкат Down. І напрямок залежностей Core ← Infrastructure.
public class MigrationTests
{
    [Fact] // самоперевірка 3: порожній файл + Migrate → усі таблиці й __EFMigrationsHistory, без правок коду
    public void Migrate_OnEmptyFile_CreatesTablesAndHistory()
    {
        using var test = new TestDatabase(migrate: false);
        using (var db = test.NewContext())
        {
            Assert.Equal(["20261116173406_InitialCreate", "20261116173414_AddLoans"], db.Database.GetPendingMigrations());
            db.Database.Migrate();
            Assert.Empty(db.Database.GetPendingMigrations());
        }

        Assert.Equal(["Books", "Copies", "Loans", "__EFMigrationsHistory"],
            Query(test, "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' " +
                        "AND name <> '__EFMigrationsLock' ORDER BY name"));
    }

    [Fact] // 1:N видно в схемі: зовнішні ключі Copies → Books (CASCADE) і Loans → Copies (RESTRICT)
    public void Schema_HasForeignKeysWithDeleteBehaviors()
    {
        using var test = new TestDatabase();

        Assert.Equal(["Books|BookId|Id|CASCADE"], Query(test,
            "SELECT \"table\" || '|' || \"from\" || '|' || \"to\" || '|' || on_delete FROM pragma_foreign_key_list('Copies')"));
        Assert.Equal(["Copies|CopyId|Id|RESTRICT"], Query(test,
            "SELECT \"table\" || '|' || \"from\" || '|' || \"to\" || '|' || on_delete FROM pragma_foreign_key_list('Loans')"));
        Assert.Contains("IX_Books_IsbnKey|1", Query(test,
            "SELECT name || '|' || \"unique\" FROM pragma_index_list('Books')"));
    }

    [Fact] // Down: відкат до InitialCreate прибирає лише Loans; Books і Copies з даними лишаються
    public async Task MigrateDown_ToInitialCreate_DropsOnlyLoans()
    {
        using var test = new TestDatabase();
        using (var db = test.NewContext())
        {
            await DbSeeder.SeedSampleDataAsync(db);
            await db.Database.GetService<IMigrator>().MigrateAsync("InitialCreate");
        }

        Assert.DoesNotContain("Loans", Query(test, "SELECT name FROM sqlite_master WHERE type='table'"));
        Assert.Equal(["19"], Query(test, "SELECT COUNT(*) FROM Books"));
        Assert.Equal(["20261116173406_InitialCreate"], Query(test, "SELECT MigrationId FROM __EFMigrationsHistory"));
    }

    [Fact] // самоперевірка 1 і напрямок залежностей: Core не посилається на EF Core ні пакетом, ні збіркою
    public void Core_DoesNotDependOnEntityFramework()
    {
        Assembly core = typeof(Core.Domain.Book).Assembly;
        Assert.DoesNotContain(core.GetReferencedAssemblies(),
            a => a.Name!.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));

        string csproj = File.ReadAllText(Path.Combine(SolutionRoot(), "src", "Core", "Core.csproj"));
        Assert.DoesNotContain("EntityFrameworkCore", csproj);
        Assert.DoesNotContain("PackageReference", csproj);
    }

    private static List<string> Query(TestDatabase test, string sql)
    {
        using var connection = new SqliteConnection(test.ConnectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        using var reader = command.ExecuteReader();
        var rows = new List<string>();
        while (reader.Read()) rows.Add(reader.GetValue(0).ToString()!);
        return rows;
    }

    private static string SolutionRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "CrossApp.sln")))
            dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("CrossApp.sln не знайдено.");
    }
}

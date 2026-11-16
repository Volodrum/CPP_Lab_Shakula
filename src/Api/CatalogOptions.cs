namespace Api;

// Секція "Catalog" в appsettings.json.
//   Seed=true — порожня БД при старті отримує стартовий каталог SampleData (19 видань, 52 примірники, 21 видача).
// Тиждень 11: вибір Memory | File прибрано — за API тепер одне сховище, SQLite (ConnectionStrings:AppDb).
// Перевизначення змінною середовища: Catalog__Seed=false.
public sealed class CatalogOptions
{
    public const string Section = "Catalog";

    public bool Seed { get; init; } = true;
}

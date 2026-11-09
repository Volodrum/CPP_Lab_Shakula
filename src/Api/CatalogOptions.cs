namespace Api;

// Секція "Catalog" в appsettings.json: яке сховище стоїть за API.
//   Memory — InMemoryBookStore (дані зникають після зупинки); Seed=true — стартові 19 видань з SampleData;
//   File   — FileBookStore тижня 9, шлях — із секції "Storage" (StorageOptions з Core).
// Перевизначення змінною середовища: Catalog__Store=File.
public sealed class CatalogOptions
{
    public const string Section = "Catalog";

    public CatalogStoreKind Store { get; init; } = CatalogStoreKind.Memory;
    public bool Seed { get; init; } = true;
}

public enum CatalogStoreKind
{
    Memory,
    File
}

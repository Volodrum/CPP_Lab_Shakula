using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

// Сховище в пам'яті: словник + початкові дані через конструктор. Після виходу з програми дані зникають.
public sealed class InMemoryBookStore(IEnumerable<ILibraryEntity>? seed = null) : IBookStore
{
    private readonly Dictionary<string, ILibraryEntity> _items =
        (seed ?? []).ToDictionary(e => e.Id, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<T> List<T>() where T : class, ILibraryEntity => _items.Values.OfType<T>().ToList();

    public T? GetById<T>(string id) where T : class, ILibraryEntity => _items.GetValueOrDefault(id) as T;

    public void Update(ILibraryEntity item) => _items[item.Id] = item;

    public bool Remove(string id) => _items.Remove(id);

    public void Add(ILibraryEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (_items.ContainsKey(item.Id))
            throw new InvalidOperationException($"Запис з id={item.Id} уже існує.");
        _items.Add(item.Id, item);
    }
}

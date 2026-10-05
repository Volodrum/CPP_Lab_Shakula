using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

// Декоратор (додаткове завдання 1): приймає будь-яке інше IBookStore і запам'ятовує результати читань.
// Той самий контракт, нова поведінка: ні сервіс, ні внутрішнє сховище про кеш не знають.
// Зміни (Add/Update/Remove) завжди йдуть у внутрішнє сховище, після чого кеш оновлюється.
public sealed class CachingBookStore(IBookStore inner) : IBookStore
{
    private readonly IBookStore _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly Dictionary<string, ILibraryEntity?> _byId = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<Type, object> _lists = [];

    public int Hits { get; private set; }
    public int Misses { get; private set; }

    public IReadOnlyList<T> List<T>() where T : class, ILibraryEntity
    {
        if (_lists.TryGetValue(typeof(T), out object? cached))
        {
            Hits++;
            return (IReadOnlyList<T>)cached;
        }

        Misses++;
        // Один список віддається багатьом викликачам, тому обгортка лише для читання: (List<T>) не спрацює.
        IReadOnlyList<T> list = _inner.List<T>().ToList().AsReadOnly();
        _lists[typeof(T)] = list;
        return list;
    }

    public T? GetById<T>(string id) where T : class, ILibraryEntity
    {
        if (_byId.TryGetValue(id, out ILibraryEntity? cached))
        {
            Hits++;
            return cached as T;
        }

        // Кешуємо сам запис, а не «запис типу T»: інакше промах GetById<Book>("C-1") сховав би примірник C-1.
        Misses++;
        ILibraryEntity? item = _inner.GetById<ILibraryEntity>(id);
        _byId[id] = item;
        return item as T;
    }

    public void Add(ILibraryEntity item)
    {
        _inner.Add(item);
        Remember(item);
    }

    public void Update(ILibraryEntity item)
    {
        _inner.Update(item);
        Remember(item);
    }

    public bool Remove(string id)
    {
        bool removed = _inner.Remove(id);
        _byId.Remove(id);
        _lists.Clear();
        return removed;
    }

    private void Remember(ILibraryEntity item)
    {
        _byId[item.Id] = item;
        _lists.Clear(); // списки могли змінитися — наступне List<T> перечитає внутрішнє сховище
    }

    public override string ToString() => $"{nameof(CachingBookStore)} → {_inner.GetType().Name}";
}

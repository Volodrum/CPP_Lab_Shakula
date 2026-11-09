using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

// Тиждень 10: декоратор потокобезпечності. Api реєструє сховище як Singleton, і його одночасно смикають
// кілька HTTP-запитів; Dictionary всередині InMemoryBookStore / FileBookStore до цього не готовий.
// Кожен виклик — під одним lock, тож внутрішнє сховище бачить запити строго по черзі.
// Атомарність кількох кроків («перевірити ISBN, потім додати») — справа CatalogService, а не сховища.
public sealed class SynchronizedBookStore(IBookStore inner) : IBookStore
{
    private readonly IBookStore _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly object _sync = new(); // Core також збирається під net8.0, де System.Threading.Lock немає

    public IReadOnlyList<T> List<T>() where T : class, ILibraryEntity
    {
        lock (_sync) return _inner.List<T>(); // і InMemory, і File повертають нову копію списку
    }

    public T? GetById<T>(string id) where T : class, ILibraryEntity
    {
        lock (_sync) return _inner.GetById<T>(id);
    }

    public void Add(ILibraryEntity item)
    {
        lock (_sync) _inner.Add(item);
    }

    public void Update(ILibraryEntity item)
    {
        lock (_sync) _inner.Update(item);
    }

    public bool Remove(string id)
    {
        lock (_sync) return _inner.Remove(id);
    }

    public override string ToString() => $"{nameof(SynchronizedBookStore)} → {_inner}";
}

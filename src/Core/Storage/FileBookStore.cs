using Core.Abstractions;
using Core.Domain;

namespace Core.Storage;

// Файлове сховище: той самий контракт, інша механіка — кеш у пам'яті, дозавантаження при першому
// зверненні, запис на диск після кожної зміни. Видання, примірники й видачі лежать в одному
// JSON-списку LibraryItemDto; тип кожного елемента — у полі "kind".
// Тиждень 9: шлях приходить ззовні (StorageOptions.ResolveFullPath у Cli), формат і атомарний запис — у LibraryJson.
public sealed class FileBookStore(string path) : IBookStore
{
    private readonly Dictionary<string, ILibraryEntity> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path = Path.GetFullPath(path);
    private bool _loaded;

    public string FilePath => _path;

    private void EnsureLoaded()
    {
        if (_loaded) return;
        if (File.Exists(_path))
            foreach (var item in LibraryJson.Read(_path)) // зіпсований файл → StorageException, кеш лишається порожнім
                _cache.Add(item.Id, item);
        _loaded = true;
    }

    // Файл уміє зберегти лише три типи; інший тип відхиляємо до зміни кешу, а не губимо мовчки під час Flush.
    private static void EnsureSupported(ILibraryEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is not (Book or BookCopy or Loan))
            throw new NotSupportedException($"Тип {item.GetType().Name} не зберігається у файлі.");
    }

    // На диск ідуть DTO, а не сутності: формат файлу не диктує форму домену. Запис — через .tmp + File.Move.
    private void Flush() => LibraryJson.Write(_path, _cache.Values);

    public IReadOnlyList<T> List<T>() where T : class, ILibraryEntity
    {
        EnsureLoaded();
        return _cache.Values.OfType<T>().ToList();
    }

    public T? GetById<T>(string id) where T : class, ILibraryEntity
    {
        EnsureLoaded();
        return _cache.GetValueOrDefault(id) as T;
    }

    public void Add(ILibraryEntity item)
    {
        EnsureSupported(item);
        EnsureLoaded();
        if (_cache.ContainsKey(item.Id))
            throw new InvalidOperationException($"Запис з id={item.Id} уже існує.");
        _cache.Add(item.Id, item);
        Flush();
    }

    public void Update(ILibraryEntity item)
    {
        EnsureSupported(item);
        EnsureLoaded();
        _cache[item.Id] = item;
        Flush();
    }

    public bool Remove(string id)
    {
        EnsureLoaded();
        if (!_cache.Remove(id)) return false;
        Flush();
        return true;
    }
}

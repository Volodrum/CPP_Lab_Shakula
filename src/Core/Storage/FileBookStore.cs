using System.Text.Json;
using Core.Abstractions;
using Core.Domain;
using Core.Dto;

namespace Core.Storage;

// Файлове сховище: той самий контракт, інша механіка — кеш у пам'яті, дозавантаження при першому
// зверненні, запис на диск після кожної зміни. Видання, примірники й видачі лежать в одному
// JSON-списку LibraryItemDto; тип кожного елемента — у полі "kind".
public sealed class FileBookStore(string path) : IBookStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly Dictionary<string, ILibraryEntity> _cache = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _path = Path.GetFullPath(path);
    private bool _loaded;

    private void EnsureLoaded()
    {
        if (_loaded) return;
        if (File.Exists(_path))
        {
            var dtos = JsonSerializer.Deserialize<List<LibraryItemDto>>(File.ReadAllText(_path), Options) ?? [];

            // Порядок важливий: видача посилається на примірник, тому спершу відновлюємо примірники.
            foreach (var dto in dtos.OfType<BookDto>()) Put(Book.FromDto(dto));
            foreach (var dto in dtos.OfType<BookCopyDto>()) Put(BookCopy.FromDto(dto));
            foreach (var dto in dtos.OfType<LoanDto>())
            {
                var copy = _cache.GetValueOrDefault(dto.CopyId) as BookCopy
                    ?? throw new InvalidDataException(
                        $"{_path}: видача {dto.Id} посилається на відсутній примірник {dto.CopyId}.");
                Put(Loan.FromDto(dto, copy));
            }
        }
        _loaded = true;
    }

    private void Put(ILibraryEntity item)
    {
        if (!_cache.TryAdd(item.Id, item))
            throw new InvalidDataException($"{_path}: id={item.Id} трапляється у файлі двічі.");
    }

    // Файл уміє зберегти лише три типи; інший тип відхиляємо до зміни кешу, а не губимо мовчки під час Flush.
    private static void EnsureSupported(ILibraryEntity item)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (item is not (Book or BookCopy or Loan))
            throw new NotSupportedException($"Тип {item.GetType().Name} не зберігається у файлі.");
    }

    private void Flush()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);

        // На диск ідуть DTO, а не сутності: формат файлу не диктує форму домену.
        List<LibraryItemDto> dtos =
        [
            .. _cache.Values.OfType<Book>().Select(b => b.ToDto()),
            .. _cache.Values.OfType<BookCopy>().Select(c => c.ToDto()),
            .. _cache.Values.OfType<Loan>().Select(l => l.ToDto())
        ];
        File.WriteAllText(_path, JsonSerializer.Serialize(dtos, Options));
    }

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

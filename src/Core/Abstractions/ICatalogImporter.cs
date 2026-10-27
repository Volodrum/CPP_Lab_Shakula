using Core.Domain;

namespace Core.Abstractions;

// Тиждень 8: асинхронний контракт імпорту каталогу видань. Читання файлу (а згодом — HTTP, БД) — це очікування
// введення-виведення, тому Task: потік не блокується, поки диск віддає байти.
// Токен скасування — частина контракту: реалізація зобов'язана передати його далі і перевіряти в циклі.
public interface ICatalogImporter
{
    // Усі видання з джерела або виняток: DomainException — запис порушує правила Book,
    // StorageException / IOException — джерело технічно непридатне, OperationCanceledException — скасовано.
    Task<IReadOnlyList<Book>> LoadAsync(string path, CancellationToken ct = default);
}

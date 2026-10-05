using Core.Domain;

namespace Core.Abstractions;

// Контракт сховища бібліотеки: лише операції, потрібні LendingService, і жодного слова про файл чи словник.
// Одне сховище тримає видання, примірники й видачі; T обирає, з якою сутністю працюємо.
public interface IBookStore
{
    // Усі записи типу T. IReadOnlyList — щоб зовнішній код не мутував сховище в обхід Add/Update/Remove.
    IReadOnlyList<T> List<T>() where T : class, ILibraryEntity;

    // Запис за id або null, якщо такого немає (чи він іншого типу). Відсутність — нормальний результат пошуку.
    T? GetById<T>(string id) where T : class, ILibraryEntity;

    // Новий запис; дубль id — InvalidOperationException (унікальність — умова щодо набору, її перевіряє сховище).
    void Add(ILibraryEntity item);

    // Зберегти змінений стан сутності (після Issue / Close / MarkLost).
    void Update(ILibraryEntity item);

    // Вилучити запис; false, якщо його не було.
    bool Remove(string id);
}

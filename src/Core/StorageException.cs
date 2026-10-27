namespace Core;

// Технічна помилка джерела даних (пошкоджений JSON). Не доменна: правила бібліотеки тут ні до чого.
// Початковий виняток зберігається в InnerException — там позиція помилки (рядок, байт), потрібна для діагностики.
public sealed class StorageException(string message, Exception inner) : Exception(message, inner);

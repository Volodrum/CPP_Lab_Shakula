using Core.Events;

namespace Core.Abstractions;

// Додаткове завдання 1: альтернатива події — одержувач сповіщень, якого сервіс отримує через конструктор.
// Реалізації (консоль, файл) живуть у Cli: Core знає лише контракт.
public interface INotificationSink
{
    void Notify(LibraryChangedEventArgs change);
}

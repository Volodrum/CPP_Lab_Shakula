using Core.Abstractions;
using Core.Events;

// Додаткове завдання 1: одержувачі сповіщень, які Cli передає в LendingService через конструктор (ручний DI тижня 5).
// Порівняння з подією — у README, розділ «Додаткові завдання».

internal sealed class ConsoleSink : INotificationSink
{
    public void Notify(LibraryChangedEventArgs change) => Console.WriteLine($" [sink] {ChangeLog.Format(change)}");
}

internal sealed class FileSink : INotificationSink
{
    private readonly string _path;

    public FileSink(string path)
    {
        _path = path;
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    }

    public void Notify(LibraryChangedEventArgs change) =>
        File.AppendAllText(_path, ChangeLog.Format(change) + Environment.NewLine);
}

// Сервіс приймає одного одержувача; щоб сповіщати двох, їх треба об'єднати вручну (подія вміє це сама: +=).
internal sealed class CompositeSink(params INotificationSink[] sinks) : INotificationSink
{
    public void Notify(LibraryChangedEventArgs change)
    {
        foreach (INotificationSink sink in sinks)
            sink.Notify(change);
    }
}

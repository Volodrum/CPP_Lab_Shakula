using Core.Events;

// Один формат рядка журналу для підписника події (Program.cs) і для ConsoleSink/FileSink (дод. завдання 1).
// Форматування — справа Cli: Core віддає лише дані зміни.
internal static class ChangeLog
{
    public static string Format(LibraryChangedEventArgs e)
    {
        string details = e.Kind switch
        {
            LibraryChangeKind.BookAdded => $"[{e.Isbn}] додано до каталогу",
            LibraryChangeKind.CopyRegistered => $"[{e.Isbn}] новий примірник на полиці",
            LibraryChangeKind.CopyIssued => $"примірник {e.CopyId} видано читачу {e.ReaderId} до {e.DueDate:dd.MM.yyyy}",
            LibraryChangeKind.CopyReturned => $"примірник {e.CopyId} повернув читач {e.ReaderId} (строк був до {e.DueDate:dd.MM.yyyy})",
            LibraryChangeKind.CopyLost => $"примірник {e.CopyId} втратив читач {e.ReaderId}",
            LibraryChangeKind.BookRemoved => $"[{e.Isbn}] вилучено з каталогу",
            _ => e.Kind.ToString()
        };

        return $"{e.At:HH:mm:ss} {e.Kind,-14} {e.EntityId,-10} {e.ShelfDelta,3:+0;-0} → на полиці {e.OnShelfAfter}  {details}";
    }
}

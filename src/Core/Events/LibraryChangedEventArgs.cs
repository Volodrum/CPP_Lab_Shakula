namespace Core.Events;

// Дані події «у бібліотеці щось змінилось». Незмінні: підписник може лише прочитати факт, а не підправити його.
// CopyId / ReaderId / DueDate заповнені для операцій з примірником або видачею, для видання — null.
public sealed class LibraryChangedEventArgs(
    LibraryChangeKind kind,
    string entityId,
    string isbn,
    int shelfDelta,
    int onShelfAfter,
    string? copyId = null,
    string? readerId = null,
    DateOnly? dueDate = null) : EventArgs
{
    public LibraryChangeKind Kind { get; } = kind;
    public string EntityId { get; } = entityId;         // id зміненого запису: B-… / C-… / L-…
    public string Isbn { get; } = isbn;
    public int ShelfDelta { get; } = shelfDelta;         // на скільки змінилась кількість примірників видання на полиці
    public int OnShelfAfter { get; } = onShelfAfter;     // скільки їх на полиці після зміни
    public string? CopyId { get; } = copyId;
    public string? ReaderId { get; } = readerId;
    public DateOnly? DueDate { get; } = dueDate;         // до якої дати читач має повернути примірник
    public DateTimeOffset At { get; } = DateTimeOffset.Now;
}

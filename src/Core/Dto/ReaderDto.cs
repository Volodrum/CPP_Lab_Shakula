namespace Core.Dto;

public sealed record ReaderDto(
    string Id,
    string FullName,
    string TicketNumber,
    string? Phone = null) : LibraryItemDto(Id);

namespace Core.Dto;

public sealed record BookCopyDto(
    string Id,
    string Isbn,
    bool IsIssued) : LibraryItemDto(Id);

namespace Core.Dto;

public sealed record LoanDto(
    string Id,
    string CopyId,
    string ReaderId,
    DateOnly IssuedOn,
    DateOnly? ReturnedOn,
    string Status) : LibraryItemDto(Id);

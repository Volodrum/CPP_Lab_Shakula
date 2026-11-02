namespace Core.Dto;

public sealed record LoanDto(
    string Id,
    string CopyId,
    string ReaderId,
    DateOnly IssuedOn,
    DateOnly? ReturnedOn,
    string Status,
    int LoanDays = 14) : LibraryItemDto(Id); // у файлах до тижня 9 поля loanDays немає — тоді строк за замовчуванням

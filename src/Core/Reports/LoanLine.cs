namespace Core.Reports;

// Рядок звіту про прострочені видачі. DaysOverdue рахується на дату звіту, у Loan його немає.
public sealed record LoanLine(string LoanId, string CopyId, string ReaderId, DateOnly IssuedOn, DateOnly DueOn, int DaysOverdue);

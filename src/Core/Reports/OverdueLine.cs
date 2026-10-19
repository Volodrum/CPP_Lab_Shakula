namespace Core.Reports;

// Дод. завдання 1: зведений рядок з двох колекцій — видача (Loan) × видання (Book).
public sealed record OverdueLine(string LoanId, string ReaderId, string Isbn, string Title, DateOnly DueOn, int DaysOverdue);

namespace Core.Reports;

// Рядок топу читачів: скільки видач було всього і скільки ще не закрито.
public sealed record ReaderStat(string ReaderId, int Loans, int ActiveLoans);

namespace Core.Reports;

// Рядок звіту про фонд: одне видання та його примірники. Контракт звіту, а не сутність:
// Book не знає, скільки в нього примірників, — ці числа з'являються лише в проєкції.
public sealed record StockLine(string BookId, string Isbn, string Title, int Year, int Copies, int OnShelf);

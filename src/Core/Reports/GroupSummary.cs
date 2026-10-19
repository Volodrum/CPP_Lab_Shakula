namespace Core.Reports;

// Підсумок однієї групи видань (тут — десятиліття року видання).
public sealed record GroupSummary(string Key, int Count, int TotalCopies);

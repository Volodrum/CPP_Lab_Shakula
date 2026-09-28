using Core.Domain;

namespace Core.Services;

// Правило, що охоплює кілька сутностей (ліміт видач на читача), живе тут, а не в Loan:
// одна видача не бачить інших видач того самого читача. На тижні 5 цю роль перейме
// CatalogService, а список _loans замінить сховище.
public sealed class LendingService
{
    public const int MaxActiveLoansPerReader = 5;

    private readonly List<Loan> _loans = [];

    public IReadOnlyList<Loan> Loans => _loans.AsReadOnly();

    public int CountActiveLoans(string readerId) =>
        _loans.Count(l => l.IsActive && string.Equals(l.ReaderId, readerId.Trim(), StringComparison.Ordinal));

    public Loan IssueCopy(BookCopy copy, string readerId, DateOnly issuedOn)
    {
        ArgumentNullException.ThrowIfNull(copy);
        if (string.IsNullOrWhiteSpace(readerId))
            throw new ArgumentException("Ідентифікатор читача обов'язковий", nameof(readerId));

        int active = CountActiveLoans(readerId);
        if (active >= MaxActiveLoansPerReader)
            throw new InvalidOperationException(
                $"Читач {readerId.Trim()} уже має {active} незакритих видач (ліміт {MaxActiveLoansPerReader}), " +
                $"видати примірник {copy.Id} неможливо");

        Loan loan = Loan.Open($"L-{_loans.Count + 1:000}", copy, readerId, issuedOn);
        _loans.Add(loan);
        return loan;
    }

    public void ReturnCopy(string loanId, DateOnly returnedOn)
    {
        Loan loan = _loans.Find(l => string.Equals(l.Id, loanId?.Trim(), StringComparison.Ordinal))
            ?? throw new ArgumentException($"Видачу '{loanId}' не знайдено", nameof(loanId));

        loan.Close(returnedOn);
    }
}

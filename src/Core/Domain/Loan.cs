using Core.Dto;

namespace Core.Domain;

// Видача примірника читачу. Стан змінюється лише переходами Open → Lost → Closed.
public sealed class Loan
{
    public string Id { get; }
    public BookCopy Copy { get; }
    public string ReaderId { get; }
    public DateOnly IssuedOn { get; }
    public DateOnly? ReturnedOn { get; private set; }
    public LoanStatus Status { get; private set; }

    // Втрачений примірник теж рахується за читачем, доки видачу не закрито.
    public bool IsActive => Status != LoanStatus.Closed;

    private Loan(string id, BookCopy copy, string readerId, DateOnly issuedOn, DateOnly? returnedOn, LoanStatus status)
    {
        Id = id;
        Copy = copy;
        ReaderId = readerId;
        IssuedOn = issuedOn;
        ReturnedOn = returnedOn;
        Status = status;
    }

    public static Loan Open(string id, BookCopy copy, string readerId, DateOnly issuedOn)
    {
        ValidateArguments(id, copy, readerId, issuedOn);

        // Примірник сам відмовить, якщо вже виданий; об'єкт видачі тоді не створюється.
        copy.Issue();

        return new Loan(id.Trim(), copy, readerId.Trim(), issuedOn, returnedOn: null, LoanStatus.Open);
    }

    public void Close(DateOnly returnedOn)
    {
        EnsureReturnDate(IssuedOn, returnedOn);
        EnsureCanMoveTo(LoanStatus.Closed);

        Copy.Return();
        ReturnedOn = returnedOn;
        Status = LoanStatus.Closed;
    }

    public void MarkLost()
    {
        EnsureCanMoveTo(LoanStatus.Lost);

        Status = LoanStatus.Lost;
    }

    // Допустимі переходи описано в одному місці.
    private void EnsureCanMoveTo(LoanStatus target)
    {
        string? error = (Status, target) switch
        {
            (LoanStatus.Open, LoanStatus.Lost or LoanStatus.Closed) => null,
            (LoanStatus.Lost, LoanStatus.Closed) => null,
            (LoanStatus.Closed, _) => $"Видача {Id} вже закрита {ReturnedOn:dd.MM.yyyy}, змінити її стан неможливо",
            (LoanStatus.Lost, LoanStatus.Lost) => $"Примірник {Copy.Id} за видачею {Id} вже позначено як втрачений",
            _ => $"Видача {Id}: перехід {Status} → {target} не передбачено"
        };

        if (error is not null)
            throw new InvalidOperationException(error);
    }

    private static void ValidateArguments(string id, BookCopy copy, string readerId, DateOnly issuedOn)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("Ідентифікатор видачі обов'язковий", nameof(id));
        ArgumentNullException.ThrowIfNull(copy);
        if (string.IsNullOrWhiteSpace(readerId))
            throw new ArgumentException("Ідентифікатор читача обов'язковий", nameof(readerId));
        if (issuedOn == default)
            throw new ArgumentOutOfRangeException(nameof(issuedOn), issuedOn, "Дата видачі обов'язкова");
    }

    private static void EnsureReturnDate(DateOnly issuedOn, DateOnly returnedOn)
    {
        if (returnedOn < issuedOn)
            throw new ArgumentOutOfRangeException(nameof(returnedOn), returnedOn,
                $"Дата повернення {returnedOn:dd.MM.yyyy} не може бути раніше дати видачі {issuedOn:dd.MM.yyyy}");
    }

    public LoanDto ToDto() => new(Id, Copy.Id, ReaderId, IssuedOn, ReturnedOn, Status.ToString());

    // Відновлення з файлу: ті самі перевірки аргументів + узгодженість статусу, дат і примірника.
    public static Loan FromDto(LoanDto dto, BookCopy copy)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidateArguments(dto.Id, copy, dto.ReaderId, dto.IssuedOn);

        if (!string.Equals(copy.Id, dto.CopyId?.Trim(), StringComparison.Ordinal))
            throw new ArgumentException(
                $"Видача {dto.Id} посилається на примірник '{dto.CopyId}', а передано {copy.Id}", nameof(copy));

        if (!Enum.TryParse(dto.Status, ignoreCase: true, out LoanStatus status) || !Enum.IsDefined(status))
            throw new ArgumentException($"Невідомий статус видачі {dto.Id}: '{dto.Status}'", nameof(dto));

        switch (status, dto.ReturnedOn)
        {
            case (LoanStatus.Closed, null):
                throw new ArgumentException($"Закрита видача {dto.Id} не має дати повернення", nameof(dto));
            case (LoanStatus.Closed, DateOnly returnedOn):
                EnsureReturnDate(dto.IssuedOn, returnedOn);
                break;
            case (_, not null):
                throw new ArgumentException($"Незакрита видача {dto.Id} не може мати дату повернення", nameof(dto));
            case (_, null) when !copy.IsIssued:
                throw new ArgumentException(
                    $"Видача {dto.Id} активна, але примірник {copy.Id} позначено як на полиці", nameof(copy));
        }

        return new Loan(dto.Id.Trim(), copy, dto.ReaderId.Trim(), dto.IssuedOn, dto.ReturnedOn, status);
    }

    public override string ToString()
    {
        string state = Status switch
        {
            LoanStatus.Open => "відкрита",
            LoanStatus.Lost => "примірник втрачено",
            LoanStatus.Closed => $"закрита {ReturnedOn:dd.MM.yyyy}",
            _ => Status.ToString()
        };
        return $"{Id}: примірник {Copy.Id} → читач {ReaderId}, видано {IssuedOn:dd.MM.yyyy}, {state}";
    }
}

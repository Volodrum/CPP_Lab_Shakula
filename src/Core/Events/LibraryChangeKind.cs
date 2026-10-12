namespace Core.Events;

// Які зміни стану бібліотеки вважаються подіями: кожна відповідає одній успішній операції LendingService.
public enum LibraryChangeKind
{
    BookAdded,
    CopyRegistered,
    CopyIssued,
    CopyReturned,
    CopyLost,
    BookRemoved
}

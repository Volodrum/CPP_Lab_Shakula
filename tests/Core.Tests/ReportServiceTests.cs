using Core.Domain;
using Core.Reports;

namespace Core.Tests;

// Звіти тижня 7 — чисті функції: на вході масиви з TestData, на виході DTO. Сховище не потрібне взагалі.
public class ReportServiceTests
{
    [Theory] // один тест — кілька наборів: межа «менше threshold» перевіряється з обох боків
    [InlineData(0, 0)]
    [InlineData(1, 2)]  // B-LOW і B-NONE (на полиці 0)
    [InlineData(4, 3)]  // + B-MID (3)
    [InlineData(5, 4)]  // + B-BIG (4)
    public void LowStock_CountsOnlyBelowThreshold(int threshold, int expected) =>
        Assert.Equal(expected, ReportService.LowStock(TestData.Books(), TestData.Copies(), threshold).Count);

    [Fact] // детермінований топ-N з тижня 7
    public void TopBooks_IsDeterministic() =>
        Assert.Equal(["B-BIG", "B-MID"], ReportService.TopBooks(TestData.Books(), TestData.Copies(), 2).Select(l => l.BookId));

    [Fact] // рівна кількість примірників: порядок вирішує другий ключ (BookId), а не порядок джерела
    public void TopBooks_OnTie_OrdersByBookId_RegardlessOfSourceOrder()
    {
        Book[] books = [Book.Create("B-2", TestData.MidIsbn, "Друга", 2000), Book.Create("B-1", TestData.LowIsbn, "Перша", 2000)];
        BookCopy[] copies =
        [
            BookCopy.Register("C-1", TestData.MidIsbn), BookCopy.Register("C-2", TestData.MidIsbn),
            BookCopy.Register("C-3", TestData.LowIsbn), BookCopy.Register("C-4", TestData.LowIsbn)
        ];

        var forward = ReportService.TopBooks(books, copies, 2).Select(l => l.BookId);
        var reversed = ReportService.TopBooks(books.Reverse(), copies.Reverse(), 2).Select(l => l.BookId);

        Assert.Equal(["B-1", "B-2"], forward);
        Assert.Equal(forward, reversed);
    }

    [Fact]
    public void Totals_OnTestData_ComputesAggregates() =>
        Assert.Equal((4, 9, 2.25, 5), ReportService.Totals(TestData.Books(), TestData.Copies()));

    [Fact] // Average/Max на порожньому джерелі кинули б виняток — звіт повертає нулі
    public void Totals_OnEmptySource_ReturnsZeros() =>
        Assert.Equal((0, 0, 0d, 0), ReportService.Totals([], []));
}

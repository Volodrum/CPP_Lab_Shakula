namespace Core.Dto;

public sealed record ImportResult<T>(IReadOnlyList<T> Items, IReadOnlyList<string> Errors)
{
    public int Total => Items.Count + Errors.Count;

    public double SuccessPercentage => Total == 0 ? 100.0 : (double)Items.Count / Total * 100.0;

    public double ErrorPercentage => Total == 0 ? 0.0 : (double)Errors.Count / Total * 100.0;

    public string Summary => $"Усього: {Total}, прийнято: {Items.Count}, пропущено: {Errors.Count}, помилок: {ErrorPercentage:F1}%";
}

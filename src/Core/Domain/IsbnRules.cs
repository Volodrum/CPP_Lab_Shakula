namespace Core.Domain;

// Спільне правило для Book і BookCopy: одна перевірка ISBN на весь домен.
// Тиждень 10: клас публічний — валідатор вхідного DTO в Api питає IsValid, а не дублює правило у себе.
public static class IsbnRules
{
    // Та сама перевірка, що й у Normalize, але відповідь bool замість винятку (для валідації запиту).
    public static bool IsValid(string? isbn)
    {
        if (string.IsNullOrWhiteSpace(isbn)) return false;

        string compact = Compact(isbn.Trim());
        return compact.Length switch
        {
            13 => compact.All(char.IsAsciiDigit),
            10 => compact[..9].All(char.IsAsciiDigit) && (char.IsAsciiDigit(compact[9]) || compact[9] == 'X'),
            _ => false
        };
    }

    public static string Normalize(string? isbn, string paramName)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            throw new ArgumentException("ISBN не може бути порожнім", paramName);

        string trimmed = isbn.Trim().ToUpperInvariant();

        if (!IsValid(trimmed))
            throw new ArgumentException(
                $"ISBN '{trimmed}' некоректний: очікується 10 або 13 цифр (дефіси дозволені)", paramName);

        return trimmed;
    }

    public static string Compact(string isbn) => isbn.Replace("-", "").Replace(" ", "").ToUpperInvariant();
}

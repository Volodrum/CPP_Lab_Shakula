namespace Core.Domain;

// Спільне правило для Book і BookCopy: одна перевірка ISBN на весь домен.
internal static class IsbnRules
{
    public static string Normalize(string? isbn, string paramName)
    {
        if (string.IsNullOrWhiteSpace(isbn))
            throw new ArgumentException("ISBN не може бути порожнім", paramName);

        string trimmed = isbn.Trim().ToUpperInvariant();
        string compact = trimmed.Replace("-", "").Replace(" ", "");

        bool valid = compact.Length switch
        {
            13 => compact.All(char.IsAsciiDigit),
            10 => compact[..9].All(char.IsAsciiDigit) && (char.IsAsciiDigit(compact[9]) || compact[9] == 'X'),
            _ => false
        };

        if (!valid)
            throw new ArgumentException(
                $"ISBN '{trimmed}' некоректний: очікується 10 або 13 цифр (дефіси дозволені)", paramName);

        return trimmed;
    }
}

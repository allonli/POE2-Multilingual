namespace Poe2DbLookup.Services;

public static class Poe2DbUrlBuilder
{
    public static string Build(string language, string value)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            throw new ArgumentException("language is required", nameof(language));
        }

        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("value is required", nameof(value));
        }

        return $"https://poe2db.tw/{language.Trim().ToLowerInvariant()}/{Uri.EscapeDataString(value.Trim())}";
    }
}

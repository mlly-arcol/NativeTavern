namespace NativeTavern.Helpers;

// Neutral language constants shared by desktop and mobile hosts; the WPF
// LocalizationService reuses these values so both ends stay in sync.
public static class LanguageCodes
{
    public const string Chinese = "zh-CN";
    public const string English = "en-US";

    public static string Normalize(string? languageCode) =>
        string.Equals(languageCode, English, StringComparison.OrdinalIgnoreCase) ? English : Chinese;
}

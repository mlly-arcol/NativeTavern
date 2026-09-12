namespace NativeTavern.Helpers;

// Neutral application-version access for code shared between the WPF desktop app
// and the MAUI Android app (both link these sources). Hosts that build the shared
// code into their own assembly keep the default; hosts linking it as a library can
// call Use() to report the host application version instead.
public static class AppVersion
{
    private static Version current = typeof(AppVersion).Assembly.GetName().Version ?? new Version();

    public static Version Current => current;
    public static string DisplayVersion => $"v{current.ToString(3)}";

    public static void Use(Version version) => current = version;
}

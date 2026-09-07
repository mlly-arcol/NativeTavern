namespace NativeTavern.Helpers;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NativeTavern");
    public static string DataDirectory { get; } = Path.Combine(Root, "Data");
    public static string LogsDirectory { get; } = Path.Combine(Root, "Logs");
    public static string CacheDirectory { get; } = Path.Combine(Root, "Cache");
    public static string AvatarsDirectory { get; } = Path.Combine(Root, "Avatars");
    public static string AttachmentsDirectory { get; } = Path.Combine(Root, "Attachments");
    public static string DocumentsDirectory { get; } = Path.Combine(Root, "Documents");
    public static string DatabaseFile { get; } = Path.Combine(DataDirectory, "NativeTavern.db");
    public static string LogFile { get; } = Path.Combine(LogsDirectory, "NativeTavern.log");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(AvatarsDirectory);
        Directory.CreateDirectory(AttachmentsDirectory);
        Directory.CreateDirectory(DocumentsDirectory);
    }
}

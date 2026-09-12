namespace NativeTavern.Helpers;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(ResolveApplicationDirectory(), "UserData");
    public static string DataDirectory { get; } = Path.Combine(Root, "Data");
    public static string LogsDirectory { get; } = Path.Combine(Root, "Logs");
    public static string CacheDirectory { get; } = Path.Combine(Root, "Cache");
    public static string AvatarsDirectory { get; } = Path.Combine(Root, "Avatars");
    public static string AttachmentsDirectory { get; } = Path.Combine(Root, "Attachments");
    public static string DocumentsDirectory { get; } = Path.Combine(Root, "Documents");
    public static string PluginsDirectory { get; } = Path.Combine(Root, "Plugins");
    public static string PluginDataDirectory { get; } = Path.Combine(Root, "PluginData");
    public static string DatabaseFile { get; } = Path.Combine(DataDirectory, "NativeTavern.db");
    public static string LogFile { get; } = Path.Combine(LogsDirectory, "NativeTavern.log");

    private static string ResolveApplicationDirectory()
    {
        var applicationDirectory = Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory);
        var parent = Directory.GetParent(applicationDirectory)?.FullName;
        return string.Equals(Path.GetFileName(applicationDirectory), "publish", StringComparison.OrdinalIgnoreCase) &&
               parent is not null && File.Exists(Path.Combine(parent, "NativeTavern.csproj"))
            ? parent
            : applicationDirectory;
    }

    public static void EnsureCreated()
    {
        MigrateLegacyData();
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(CacheDirectory);
        Directory.CreateDirectory(AvatarsDirectory);
        Directory.CreateDirectory(AttachmentsDirectory);
        Directory.CreateDirectory(DocumentsDirectory);
        Directory.CreateDirectory(PluginsDirectory);
        Directory.CreateDirectory(PluginDataDirectory);
    }

    private static void MigrateLegacyData()
    {
        if (File.Exists(DatabaseFile)) return;

        var source = GetLegacyRoots()
            .Where(path => File.Exists(Path.Combine(path, "Data", "NativeTavern.db")))
            .OrderByDescending(path =>
                Directory.Exists(Path.Combine(path, "Avatars")) &&
                Directory.EnumerateFiles(Path.Combine(path, "Avatars")).Any())
            .ThenByDescending(path =>
                File.GetLastWriteTimeUtc(Path.Combine(path, "Data", "NativeTavern.db")))
            .FirstOrDefault();
        if (source is null) return;

        CopyDirectory(source, Root);
    }

    private static IEnumerable<string> GetLegacyRoots()
    {
        var packagesRoot = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "AppData", "Local", "Packages");
        if (Directory.Exists(packagesRoot))
        {
            foreach (var package in Directory.EnumerateDirectories(packagesRoot, "OpenAI.Codex_*"))
            {
                yield return Path.Combine(
                    package, "LocalCache", "Local", "NativeTavern");
            }
        }

        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NativeTavern");
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(
                     source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination, Path.GetRelativePath(source, directory)));
        }

        foreach (var file in Directory.EnumerateFiles(
                     source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            // Overwrite so a previously interrupted migration cannot crash the next startup.
            File.Copy(file, target, true);
        }
    }
}

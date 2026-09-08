using Microsoft.Extensions.Logging;

namespace NativeTavern.Helpers;

public static class ManagedFile
{
    public static bool IsInsideDirectory(string path, string directory)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(directory)) return false;
        try
        {
            var relative = Path.GetRelativePath(Path.GetFullPath(directory), Path.GetFullPath(path));
            return !Path.IsPathRooted(relative) &&
                   relative != ".." &&
                   !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
                   !relative.StartsWith(".." + Path.AltDirectorySeparatorChar, StringComparison.Ordinal);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    public static void TryDelete(string path, string directory, ILogger logger)
    {
        if (!IsInsideDirectory(path, directory) || !File.Exists(path)) return;
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(ex, "Could not remove managed file {ManagedFilePath}.", path);
        }
    }
}

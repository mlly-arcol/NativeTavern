using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NativeTavern.Helpers;

namespace NativeTavern.Services;

public sealed class BackupService
{
    private static readonly string[] ManagedDirectories = ["Avatars", "Attachments", "Documents"];
    private static readonly JsonSerializerOptions ManifestJsonOptions = new() { WriteIndented = true };
    private readonly string root;
    private readonly string databaseFile;

    public BackupService(string? userDataRoot = null)
    {
        root = Path.GetFullPath(userDataRoot ?? AppPaths.Root);
        databaseFile = Path.Combine(root, "Data", "NativeTavern.db");
    }

    public async Task CreateAsync(string destinationPath)
    {
        if (!File.Exists(databaseFile)) throw new FileNotFoundException("找不到 NativeTavern 数据库。", databaseFile);
        var destination = Path.GetFullPath(destinationPath);
        if (IsInsideRoot(destination)) throw new InvalidOperationException("备份文件不能保存在 UserData 文件夹内。");

        var temporary = CreateTemporaryDirectory();
        string? temporaryArchive = null;
        try
        {
            var backupRoot = Path.Combine(temporary, "UserData");
            Directory.CreateDirectory(Path.Combine(backupRoot, "Data"));
            await SnapshotDatabaseAsync(Path.Combine(backupRoot, "Data", "NativeTavern.db"));
            foreach (var name in ManagedDirectories)
            {
                var managedBackupDirectory = Path.Combine(backupRoot, name);
                Directory.CreateDirectory(managedBackupDirectory);
                CopyDirectory(Path.Combine(root, name), managedBackupDirectory);
            }

            await File.WriteAllTextAsync(
                Path.Combine(temporary, "backup.json"),
                JsonSerializer.Serialize(new
                {
                    format = "NativeTavernBackup",
                    version = 1,
                    applicationVersion = App.DisplayVersion,
                    createdAt = DateTimeOffset.UtcNow
                }, ManifestJsonOptions));

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            temporaryArchive = destination + "." + Guid.NewGuid().ToString("N") + ".tmp";
            ZipFile.CreateFromDirectory(temporary, temporaryArchive, CompressionLevel.Optimal, false);
            File.Move(temporaryArchive, destination, true);
            temporaryArchive = null;
        }
        finally
        {
            if (temporaryArchive is not null && File.Exists(temporaryArchive)) File.Delete(temporaryArchive);
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    public async Task<string> RestoreAsync(string backupPath)
    {
        var source = Path.GetFullPath(backupPath);
        if (!File.Exists(source)) throw new FileNotFoundException("找不到备份文件。", source);
        var temporary = CreateTemporaryDirectory();
        try
        {
            ValidateArchive(source, temporary);
            ZipFile.ExtractToDirectory(source, temporary);
            var restoredRoot = Path.Combine(temporary, "UserData");
            var restoredDatabase = Path.Combine(restoredRoot, "Data", "NativeTavern.db");
            foreach (var name in ManagedDirectories)
                if (!Directory.Exists(Path.Combine(restoredRoot, name)))
                    throw new InvalidDataException($"备份中缺少 {name} 文件夹。");
            await ValidateDatabaseAsync(restoredDatabase);

            var safetyBackup = Path.Combine(
                Directory.GetParent(root)?.FullName ?? root,
                $"NativeTavern-before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            await CreateAsync(safetyBackup);

            SqliteConnection.ClearAllPools();
            Directory.CreateDirectory(Path.GetDirectoryName(databaseFile)!);
            File.Copy(restoredDatabase, databaseFile, true);
            foreach (var name in ManagedDirectories)
                ReplaceDirectory(Path.Combine(restoredRoot, name), Path.Combine(root, name));
            return safetyBackup;
        }
        finally
        {
            if (Directory.Exists(temporary)) Directory.Delete(temporary, true);
        }
    }

    private async Task SnapshotDatabaseAsync(string destination)
    {
        await using var source = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = databaseFile,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        await using var target = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = destination,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Pooling = false
        }.ToString());
        await source.OpenAsync();
        await target.OpenAsync();
        source.BackupDatabase(target);
    }

    private static async Task ValidateDatabaseAsync(string path)
    {
        if (!File.Exists(path)) throw new InvalidDataException("备份中缺少 Data/NativeTavern.db。");
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check";
        var result = await command.ExecuteScalarAsync() as string;
        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("备份数据库完整性检查失败。");
    }

    private bool IsInsideRoot(string path) =>
        path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    private static string CreateTemporaryDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "NativeTavern-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void ValidateArchive(string archivePath, string extractionDirectory)
    {
        const long maxExpandedBytes = 16L * 1024 * 1024 * 1024;
        const int maxEntries = 100_000;
        using var archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > maxEntries)
            throw new InvalidDataException("备份包含过多文件。");
        if (archive.GetEntry("backup.json") is null)
            throw new InvalidDataException("不是有效的 NativeTavern 备份。");

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(extractionDirectory)) +
                   Path.DirectorySeparatorChar;
        long expandedBytes = 0;
        foreach (var entry in archive.Entries)
        {
            if (entry.Length > maxExpandedBytes - expandedBytes)
                throw new InvalidDataException("备份解压后的大小超过 16 GB 限制。");
            expandedBytes += entry.Length;
            var target = Path.GetFullPath(Path.Combine(extractionDirectory, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("备份包含不安全的文件路径。");
        }
    }

    private static void CopyDirectory(string source, string destination, bool overwrite = false)
    {
        if (!Directory.Exists(source)) return;
        Directory.CreateDirectory(destination);
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite);
        }
    }

    private static void ReplaceDirectory(string source, string destination)
    {
        if (!Directory.Exists(source))
            throw new InvalidDataException($"备份中缺少 {Path.GetFileName(destination)} 文件夹。");
        if (Directory.Exists(destination)) Directory.Delete(destination, true);
        CopyDirectory(source, destination);
    }
}

using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NativeTavern.Helpers;

namespace NativeTavern.Services;

public sealed class BackupService
{
    private static readonly string[] ManagedDirectories = ["Avatars", "Attachments", "Documents"];
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
        try
        {
            var backupRoot = Path.Combine(temporary, "UserData");
            Directory.CreateDirectory(Path.Combine(backupRoot, "Data"));
            await SnapshotDatabaseAsync(Path.Combine(backupRoot, "Data", "NativeTavern.db"));
            foreach (var name in ManagedDirectories)
                CopyDirectory(Path.Combine(root, name), Path.Combine(backupRoot, name));

            await File.WriteAllTextAsync(
                Path.Combine(temporary, "backup.json"),
                JsonSerializer.Serialize(new
                {
                    format = "NativeTavernBackup",
                    version = 1,
                    applicationVersion = App.DisplayVersion,
                    createdAt = DateTimeOffset.UtcNow
                }, new JsonSerializerOptions { WriteIndented = true }));

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            if (File.Exists(destination)) File.Delete(destination);
            ZipFile.CreateFromDirectory(temporary, destination, CompressionLevel.Optimal, false);
        }
        finally
        {
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
            ZipFile.ExtractToDirectory(source, temporary);
            var restoredRoot = Path.Combine(temporary, "UserData");
            var restoredDatabase = Path.Combine(restoredRoot, "Data", "NativeTavern.db");
            await ValidateDatabaseAsync(restoredDatabase);

            var safetyBackup = Path.Combine(
                Directory.GetParent(root)?.FullName ?? root,
                $"NativeTavern-before-restore-{DateTime.Now:yyyyMMdd-HHmmss}.zip");
            await CreateAsync(safetyBackup);

            SqliteConnection.ClearAllPools();
            Directory.CreateDirectory(Path.GetDirectoryName(databaseFile)!);
            File.Copy(restoredDatabase, databaseFile, true);
            foreach (var name in ManagedDirectories)
                CopyDirectory(Path.Combine(restoredRoot, name), Path.Combine(root, name), overwrite: true);
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
}

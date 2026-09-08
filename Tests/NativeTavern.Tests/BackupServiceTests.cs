using Microsoft.Data.Sqlite;
using NativeTavern.Services;
using System.IO.Compression;
using Xunit;

namespace NativeTavern.Tests;

public sealed class BackupServiceTests
{
    [Fact]
    public async Task BackupAndRestoreRoundTripDatabaseAndManagedFiles()
    {
        var parent = Path.Combine(Path.GetTempPath(), "NativeTavernBackupTest-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "UserData");
        var database = Path.Combine(root, "Data", "NativeTavern.db");
        var avatar = Path.Combine(root, "Avatars", "avatar.png");
        var backup = Path.Combine(parent, "backup.zip");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(database)!);
            Directory.CreateDirectory(Path.GetDirectoryName(avatar)!);
            await ExecuteAsync(database, "CREATE TABLE Sample(Value TEXT); INSERT INTO Sample VALUES('before');");
            await File.WriteAllTextAsync(avatar, "avatar-before");
            var service = new BackupService(root);

            await service.CreateAsync(backup);
            await ExecuteAsync(database, "DELETE FROM Sample; INSERT INTO Sample VALUES('after');");
            await File.WriteAllTextAsync(avatar, "avatar-after");
            var staleAvatar = Path.Combine(root, "Avatars", "stale.png");
            await File.WriteAllTextAsync(staleAvatar, "not-in-backup");

            var safetyBackup = await service.RestoreAsync(backup);

            Assert.Equal("before", await ScalarAsync(database, "SELECT Value FROM Sample"));
            Assert.Equal("avatar-before", await File.ReadAllTextAsync(avatar));
            Assert.False(File.Exists(staleAvatar));
            Assert.True(File.Exists(safetyBackup));
        }
        finally
        {
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }

    [Fact]
    public async Task RestoreRejectsArchiveWithoutNativeTavernManifest()
    {
        var parent = Path.Combine(Path.GetTempPath(), "NativeTavernBackupTest-" + Guid.NewGuid().ToString("N"));
        var root = Path.Combine(parent, "UserData");
        var database = Path.Combine(root, "Data", "NativeTavern.db");
        var archivePath = Path.Combine(parent, "invalid.zip");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(database)!);
            await ExecuteAsync(database, "CREATE TABLE Sample(Value TEXT);");
            using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("unrelated.txt");
                await using var writer = new StreamWriter(entry.Open());
                await writer.WriteAsync("not a backup");
            }

            var service = new BackupService(root);
            await Assert.ThrowsAsync<InvalidDataException>(() => service.RestoreAsync(archivePath));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }

    private static async Task ExecuteAsync(string database, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={database};Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarAsync(string database, string sql)
    {
        await using var connection = new SqliteConnection($"Data Source={database};Mode=ReadOnly;Pooling=False");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return await command.ExecuteScalarAsync() as string;
    }
}

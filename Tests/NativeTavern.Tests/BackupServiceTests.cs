using Microsoft.Data.Sqlite;
using NativeTavern.Services;
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

            var safetyBackup = await service.RestoreAsync(backup);

            Assert.Equal("before", await ScalarAsync(database, "SELECT Value FROM Sample"));
            Assert.Equal("avatar-before", await File.ReadAllTextAsync(avatar));
            Assert.True(File.Exists(safetyBackup));
        }
        finally
        {
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

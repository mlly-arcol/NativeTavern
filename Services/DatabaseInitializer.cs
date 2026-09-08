using Dapper;
using Microsoft.Extensions.Logging;
using NativeTavern.Data;

namespace NativeTavern.Services;

public sealed class DatabaseInitializer(DatabaseConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync()
    {
        await using var schemaStream = typeof(DatabaseInitializer).Assembly
            .GetManifestResourceStream("NativeTavern.Data.Sql.schema.sql")
            ?? throw new InvalidOperationException("Embedded database schema is missing.");
        using var schemaReader = new StreamReader(schemaStream);
        var schema = await schemaReader.ReadToEndAsync();
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(schema);
        var columns = (await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('ChatMessages')")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!columns.Contains("CurrentSwipeIndex"))
        {
            await connection.ExecuteAsync(
                "ALTER TABLE ChatMessages ADD COLUMN CurrentSwipeIndex INTEGER NOT NULL DEFAULT 0");
            logger.LogInformation("Migrated ChatMessages for V0.3 swipe support.");
        }
        var sessionColumns = (await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('ChatSessions')")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var migrations = new Dictionary<string, string>
        {
            ["PersonaId"] = "ALTER TABLE ChatSessions ADD COLUMN PersonaId INTEGER NULL",
            ["LorebookId"] = "ALTER TABLE ChatSessions ADD COLUMN LorebookId INTEGER NULL",
            ["PromptPresetId"] = "ALTER TABLE ChatSessions ADD COLUMN PromptPresetId INTEGER NULL",
            ["AuthorNote"] = "ALTER TABLE ChatSessions ADD COLUMN AuthorNote TEXT NOT NULL DEFAULT ''",
            ["Summary"] = "ALTER TABLE ChatSessions ADD COLUMN Summary TEXT NOT NULL DEFAULT ''"
        };
        foreach (var migration in migrations.Where(x => !sessionColumns.Contains(x.Key)))
        {
            await connection.ExecuteAsync(migration.Value);
            logger.LogInformation("Migrated ChatSessions column {ColumnName} for V0.4.", migration.Key);
        }
        if (!sessionColumns.Contains("IsGroupChat"))
        {
            await connection.ExecuteAsync(
                "ALTER TABLE ChatSessions ADD COLUMN IsGroupChat INTEGER NOT NULL DEFAULT 0");
            logger.LogInformation("Migrated ChatSessions group chat support.");
        }
        if (!columns.Contains("SpeakerCharacterId"))
        {
            await connection.ExecuteAsync(
                "ALTER TABLE ChatMessages ADD COLUMN SpeakerCharacterId INTEGER NULL");
            logger.LogInformation("Migrated ChatMessages speaker identity support.");
        }
        await connection.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_ChatSessionCharacters_Session ON ChatSessionCharacters(ChatSessionId)");
        await connection.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_ChatMessages_SpeakerCharacterId ON ChatMessages(SpeakerCharacterId)");
        var characterColumns = (await connection.QueryAsync<string>(
            "SELECT name FROM pragma_table_info('Characters')")).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!characterColumns.Contains("GroupName"))
        {
            await connection.ExecuteAsync("ALTER TABLE Characters ADD COLUMN GroupName TEXT NOT NULL DEFAULT ''");
            logger.LogInformation("Migrated Characters grouping support.");
        }
        await connection.ExecuteAsync(
            "CREATE INDEX IF NOT EXISTS IX_Characters_GroupName ON Characters(GroupName)");
        await connection.ExecuteAsync(
            "INSERT OR IGNORE INTO CharacterGroups(Name,CreatedAt) " +
            "SELECT DISTINCT TRIM(GroupName),@createdAt FROM Characters WHERE TRIM(GroupName)<>''",
            new { createdAt = DateTimeOffset.UtcNow.ToString("O") });
        await NormalizeManagedPathsAsync(connection);
        logger.LogInformation("Database initialized at {DatabasePath}", Helpers.AppPaths.DatabaseFile);
    }

    private static async Task NormalizeManagedPathsAsync(System.Data.IDbConnection connection)
    {
        var avatars = await connection.QueryAsync<ManagedPathRow>(
            "SELECT Id, AvatarPath AS Path FROM Characters WHERE AvatarPath <> ''");
        foreach (var row in avatars)
        {
            var path = Path.Combine(Helpers.AppPaths.AvatarsDirectory, Path.GetFileName(row.Path));
            if (File.Exists(path) && !string.Equals(path, row.Path, StringComparison.OrdinalIgnoreCase))
                await connection.ExecuteAsync(
                    "UPDATE Characters SET AvatarPath=@path WHERE Id=@id",
                    new { path, id = row.Id });
        }

        var documents = await connection.QueryAsync<ManagedPathRow>(
            "SELECT Id, ManagedPath AS Path FROM KnowledgeDocuments WHERE ManagedPath <> ''");
        foreach (var row in documents)
        {
            var path = Path.Combine(Helpers.AppPaths.DocumentsDirectory, Path.GetFileName(row.Path));
            if (File.Exists(path) && !string.Equals(path, row.Path, StringComparison.OrdinalIgnoreCase))
                await connection.ExecuteAsync(
                    "UPDATE KnowledgeDocuments SET ManagedPath=@path WHERE Id=@id",
                    new { path, id = row.Id });
        }

        var attachments = await connection.QueryAsync<ManagedPathRow>(
            "SELECT Id, FilePath AS Path FROM ChatAttachments WHERE FilePath <> ''");
        foreach (var row in attachments)
        {
            var path = Path.Combine(Helpers.AppPaths.AttachmentsDirectory, Path.GetFileName(row.Path));
            if (File.Exists(path) && !string.Equals(path, row.Path, StringComparison.OrdinalIgnoreCase))
                await connection.ExecuteAsync(
                    "UPDATE ChatAttachments SET FilePath=@path WHERE Id=@id",
                    new { path, id = row.Id });
        }
    }

    private sealed class ManagedPathRow
    {
        public long Id { get; init; }
        public string Path { get; init; } = string.Empty;
    }
}

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
        logger.LogInformation("Database initialized at {DatabasePath}", Helpers.AppPaths.DatabaseFile);
    }
}

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
        logger.LogInformation("Database initialized at {DatabasePath}", Helpers.AppPaths.DatabaseFile);
    }
}

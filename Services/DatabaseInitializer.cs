using Dapper;
using Microsoft.Extensions.Logging;
using NativeTavern.Data;

namespace NativeTavern.Services;

public sealed class DatabaseInitializer(DatabaseConnectionFactory connectionFactory, ILogger<DatabaseInitializer> logger)
{
    public async Task InitializeAsync()
    {
        var schemaPath = Path.Combine(AppContext.BaseDirectory, "Data", "Sql", "schema.sql");
        if (!File.Exists(schemaPath)) throw new FileNotFoundException("Database schema file is missing.", schemaPath);
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await connection.ExecuteAsync(await File.ReadAllTextAsync(schemaPath));
        logger.LogInformation("Database initialized at {DatabasePath}", Helpers.AppPaths.DatabaseFile);
    }
}

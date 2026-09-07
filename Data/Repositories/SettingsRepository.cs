using Dapper;

namespace NativeTavern.Data.Repositories;

public sealed class SettingsRepository(DatabaseConnectionFactory connectionFactory)
{
    public async Task<string?> GetAsync(string key)
    {
        await using var connection = connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<string?>(
            "SELECT Value FROM AppSettings WHERE Key=@key", new { key });
    }

    public async Task SetAsync(string key, string value)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT INTO AppSettings(Key,Value) VALUES(@key,@value) " +
            "ON CONFLICT(Key) DO UPDATE SET Value=excluded.Value", new { key, value });
    }
}

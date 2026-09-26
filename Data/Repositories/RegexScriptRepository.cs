using Dapper;
using NativeTavern.Models;

namespace NativeTavern.Data.Repositories;

public sealed class RegexScriptRepository(DatabaseConnectionFactory connectionFactory)
{
    private sealed class Row
    {
        public long Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Pattern { get; init; } = string.Empty;
        public string Replacement { get; init; } = string.Empty;
        public string Target { get; init; } = string.Empty;
        public string Mode { get; init; } = string.Empty;
        public int IsEnabled { get; init; }
        public int SortOrder { get; init; }
        public string CreatedAt { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;

        public RegexScript ToModel() => new()
        {
            Id = Id,
            Name = Name,
            Pattern = Pattern,
            Replacement = Replacement,
            Target = (RegexScriptTarget)Enum.Parse(typeof(RegexScriptTarget), Target, true),
            Mode = (RegexScriptMode)Enum.Parse(typeof(RegexScriptMode), Mode, true),
            IsEnabled = IsEnabled != 0,
            SortOrder = SortOrder,
            CreatedAt = DateTimeOffset.Parse(CreatedAt),
            UpdatedAt = DateTimeOffset.Parse(UpdatedAt),
        };
    }

    public async Task<IReadOnlyList<RegexScript>> GetAllAsync()
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<Row>(
            "SELECT * FROM RegexScripts ORDER BY SortOrder, Id");
        return rows.Select(x => x.ToModel()).ToList();
    }

    public async Task SaveAsync(RegexScript value)
    {
        var now = DateTimeOffset.UtcNow;
        value.UpdatedAt = now;
        var stored = new
        {
            value.Name,
            value.Pattern,
            value.Replacement,
            Target = value.Target.ToString(),
            Mode = value.Mode.ToString(),
            IsEnabled = value.IsEnabled ? 1 : 0,
            value.SortOrder,
            CreatedAt = now.ToString("O"),
            UpdatedAt = now.ToString("O"),
        };
        await using var connection = connectionFactory.CreateConnection();
        if (value.Id == 0)
        {
            value.CreatedAt = now;
            value.Id = await connection.ExecuteScalarAsync<long>(
                "INSERT INTO RegexScripts(Name,Pattern,Replacement,Target,Mode,IsEnabled,SortOrder,CreatedAt,UpdatedAt) " +
                "VALUES(@Name,@Pattern,@Replacement,@Target,@Mode,@IsEnabled,@SortOrder,@CreatedAt,@UpdatedAt); SELECT last_insert_rowid();",
                stored);
        }
        else
        {
            await connection.ExecuteAsync(
                "UPDATE RegexScripts SET Name=@Name,Pattern=@Pattern,Replacement=@Replacement,Target=@Target,Mode=@Mode," +
                "IsEnabled=@IsEnabled,SortOrder=@SortOrder,UpdatedAt=@UpdatedAt WHERE Id=@Id",
                new { stored.Name, stored.Pattern, stored.Replacement, stored.Target, stored.Mode, stored.IsEnabled,
                      stored.SortOrder, stored.UpdatedAt, value.Id });
        }
    }

    public async Task DeleteAsync(long id)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM RegexScripts WHERE Id=@id", new { id });
    }
}

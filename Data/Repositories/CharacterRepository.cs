using Dapper;
using NativeTavern.Models;

namespace NativeTavern.Data.Repositories;

public sealed class CharacterRepository(DatabaseConnectionFactory connectionFactory)
{
    public async Task<long> CreateAsync(Character character)
    {
        await using var connection = connectionFactory.CreateConnection();
        character.Id = await connection.ExecuteScalarAsync<long>(
            "INSERT INTO Characters(Name,Description,Personality,Scenario,FirstMessage,ExampleMessages,Creator,Tags,IsFavorite,AvatarPath,CreatedAt,UpdatedAt) " +
            "VALUES(@Name,@Description,@Personality,@Scenario,@FirstMessage,@ExampleMessages,@Creator,@Tags,@IsFavorite,@AvatarPath,@CreatedAt,@UpdatedAt); SELECT last_insert_rowid();",
            ToParameters(character));
        return character.Id;
    }

    public async Task UpdateAsync(Character character)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Characters SET Name=@Name,Description=@Description,Personality=@Personality,Scenario=@Scenario,FirstMessage=@FirstMessage," +
            "ExampleMessages=@ExampleMessages,Creator=@Creator,Tags=@Tags,IsFavorite=@IsFavorite,AvatarPath=@AvatarPath,UpdatedAt=@UpdatedAt WHERE Id=@Id",
            ToParameters(character));
    }

    public async Task DeleteAsync(long id)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM Characters WHERE Id=@id", new { id });
    }

    public async Task<Character?> GetAsync(long id)
    {
        await using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<CharacterRow>(
            "SELECT * FROM Characters WHERE Id=@id", new { id });
        return row?.ToModel();
    }

    public async Task<IReadOnlyList<Character>> SearchAsync(string? query, bool favoritesOnly)
    {
        await using var connection = connectionFactory.CreateConnection();
        var pattern = "%" + (query ?? string.Empty).Trim() + "%";
        var rows = await connection.QueryAsync<CharacterRow>(
            "SELECT * FROM Characters WHERE (@favoritesOnly=0 OR IsFavorite=1) " +
            "AND (@pattern='%%' OR Name LIKE @pattern OR Tags LIKE @pattern OR Description LIKE @pattern) " +
            "ORDER BY IsFavorite DESC, UpdatedAt DESC",
            new { favoritesOnly = favoritesOnly ? 1 : 0, pattern });
        return rows.Select(x => x.ToModel()).ToList();
    }

    private static object ToParameters(Character value) => new
    {
        value.Id, value.Name, value.Description, value.Personality, value.Scenario,
        value.FirstMessage, value.ExampleMessages, value.Creator, value.Tags,
        IsFavorite = value.IsFavorite ? 1 : 0, value.AvatarPath,
        CreatedAt = value.CreatedAt.ToString("O"), UpdatedAt = value.UpdatedAt.ToString("O")
    };

    private sealed class CharacterRow
    {
        public long Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string Description { get; init; } = string.Empty;
        public string Personality { get; init; } = string.Empty;
        public string Scenario { get; init; } = string.Empty;
        public string FirstMessage { get; init; } = string.Empty;
        public string ExampleMessages { get; init; } = string.Empty;
        public string Creator { get; init; } = string.Empty;
        public string Tags { get; init; } = string.Empty;
        public int IsFavorite { get; init; }
        public string AvatarPath { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;
        public Character ToModel() => new()
        {
            Id = Id, Name = Name, Description = Description, Personality = Personality,
            Scenario = Scenario, FirstMessage = FirstMessage, ExampleMessages = ExampleMessages,
            Creator = Creator, Tags = Tags, IsFavorite = IsFavorite != 0, AvatarPath = AvatarPath,
            CreatedAt = DateTimeOffset.Parse(CreatedAt), UpdatedAt = DateTimeOffset.Parse(UpdatedAt)
        };
    }
}

using Dapper;
using NativeTavern.Models;

namespace NativeTavern.Data.Repositories;

public sealed class CharacterRepository(DatabaseConnectionFactory connectionFactory)
{
    public async Task<long> CreateAsync(Character character)
    {
        await using var connection = connectionFactory.CreateConnection();
        character.Id = await connection.ExecuteScalarAsync<long>(
            "INSERT INTO Characters(Name,Description,Personality,Scenario,FirstMessage,ExampleMessages,Creator,Tags,GroupName,IsFavorite,AvatarPath,CreatedAt,UpdatedAt) " +
            "VALUES(@Name,@Description,@Personality,@Scenario,@FirstMessage,@ExampleMessages,@Creator,@Tags,@GroupName,@IsFavorite,@AvatarPath,@CreatedAt,@UpdatedAt); SELECT last_insert_rowid();",
            ToParameters(character));
        return character.Id;
    }

    public async Task UpdateAsync(Character character)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE Characters SET Name=@Name,Description=@Description,Personality=@Personality,Scenario=@Scenario,FirstMessage=@FirstMessage," +
            "ExampleMessages=@ExampleMessages,Creator=@Creator,Tags=@Tags,GroupName=@GroupName,IsFavorite=@IsFavorite,AvatarPath=@AvatarPath,UpdatedAt=@UpdatedAt WHERE Id=@Id",
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

    public async Task<IReadOnlyList<Character>> GetByIdsAsync(IEnumerable<long> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return [];
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<CharacterRow>(
            "SELECT * FROM Characters WHERE Id IN @idList", new { idList });
        var byId = rows.ToDictionary(x => x.Id);
        return idList.Where(byId.ContainsKey).Select(id => byId[id].ToModel()).ToList();
    }

    public async Task<IReadOnlyList<Character>> GetByGroupAsync(string groupName)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<CharacterRow>(
            "SELECT * FROM Characters WHERE GroupName=@groupName COLLATE NOCASE ORDER BY UpdatedAt DESC",
            new { groupName });
        return rows.Select(x => x.ToModel()).ToList();
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

    public async Task<IReadOnlyList<CharacterGroup>> GetGroupsAsync()
    {
        await using var connection = connectionFactory.CreateConnection();
        var groups = await connection.QueryAsync<CharacterGroup>(
            "SELECT g.Name,COUNT(c.Id) AS CharacterCount FROM CharacterGroups g " +
            "LEFT JOIN Characters c ON c.GroupName=g.Name COLLATE NOCASE " +
            "GROUP BY g.Name ORDER BY g.Name COLLATE NOCASE");
        return groups.ToList();
    }

    public async Task EnsureGroupAsync(string name)
    {
        name = name.Trim();
        if (name.Length == 0) return;
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT OR IGNORE INTO CharacterGroups(Name,CreatedAt) VALUES(@name,@createdAt)",
            new { name, createdAt = DateTimeOffset.UtcNow.ToString("O") });
    }

    public async Task CreateGroupAsync(string name, IReadOnlyCollection<long> characterIds)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync(
            "INSERT INTO CharacterGroups(Name,CreatedAt) VALUES(@name,@createdAt)",
            new { name, createdAt = DateTimeOffset.UtcNow.ToString("O") }, transaction);
        if (characterIds.Count > 0)
            await connection.ExecuteAsync(
                "UPDATE Characters SET GroupName=@name,UpdatedAt=@updatedAt WHERE Id IN @characterIds",
                new { name, updatedAt = DateTimeOffset.UtcNow.ToString("O"), characterIds }, transaction);
        await transaction.CommitAsync();
    }

    public async Task UpdateGroupAsync(
        string originalName,
        string name,
        IReadOnlyCollection<long> characterIds)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        var updatedAt = DateTimeOffset.UtcNow.ToString("O");
        await connection.ExecuteAsync(
            "UPDATE Characters SET GroupName='',UpdatedAt=@updatedAt WHERE GroupName=@originalName COLLATE NOCASE",
            new { originalName, updatedAt }, transaction);
        await connection.ExecuteAsync(
            "UPDATE CharacterGroups SET Name=@name WHERE Name=@originalName COLLATE NOCASE",
            new { name, originalName }, transaction);
        if (characterIds.Count > 0)
            await connection.ExecuteAsync(
                "UPDATE Characters SET GroupName=@name,UpdatedAt=@updatedAt WHERE Id IN @characterIds",
                new { name, updatedAt, characterIds }, transaction);
        await transaction.CommitAsync();
    }

    public async Task DeleteGroupAsync(string name)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync(
            "UPDATE Characters SET GroupName='',UpdatedAt=@updatedAt WHERE GroupName=@name COLLATE NOCASE",
            new { name, updatedAt = DateTimeOffset.UtcNow.ToString("O") }, transaction);
        await connection.ExecuteAsync(
            "DELETE FROM CharacterGroups WHERE Name=@name COLLATE NOCASE",
            new { name }, transaction);
        await transaction.CommitAsync();
    }

    private static object ToParameters(Character value) => new
    {
        value.Id, value.Name, value.Description, value.Personality, value.Scenario,
        value.FirstMessage, value.ExampleMessages, value.Creator, value.Tags, value.GroupName,
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
        public string GroupName { get; init; } = string.Empty;
        public int IsFavorite { get; init; }
        public string AvatarPath { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;
        public Character ToModel() => new()
        {
            Id = Id, Name = Name, Description = Description, Personality = Personality,
            Scenario = Scenario, FirstMessage = FirstMessage, ExampleMessages = ExampleMessages,
            Creator = Creator, Tags = Tags, GroupName = GroupName, IsFavorite = IsFavorite != 0, AvatarPath = AvatarPath,
            CreatedAt = DateTimeOffset.Parse(CreatedAt), UpdatedAt = DateTimeOffset.Parse(UpdatedAt)
        };
    }
}

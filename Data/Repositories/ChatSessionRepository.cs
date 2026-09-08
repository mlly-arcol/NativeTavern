using Dapper;
using NativeTavern.Models;

namespace NativeTavern.Data.Repositories;

public sealed class ChatSessionRepository(DatabaseConnectionFactory connectionFactory)
{
    public async Task<long> CreateAsync(ChatSession session)
    {
        await using var connection = connectionFactory.CreateConnection();
        session.Id = await connection.ExecuteScalarAsync<long>(
            "INSERT INTO ChatSessions(Title,CharacterId,IsGroupChat,PersonaId,LorebookId,PromptPresetId,AuthorNote,Summary,CreatedAt,UpdatedAt) " +
            "VALUES(@Title,@CharacterId,@IsGroupChat,@PersonaId,@LorebookId,@PromptPresetId,@AuthorNote,@Summary,@CreatedAt,@UpdatedAt); SELECT last_insert_rowid();",
            ToParameters(session));
        return session.Id;
    }

    public async Task<ChatSession?> GetAsync(long id)
    {
        await using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<SessionRow>(
            "SELECT * FROM ChatSessions WHERE Id=@id", new { id });
        return row?.ToModel();
    }

    public async Task<IReadOnlyList<ChatSession>> GetAllAsync()
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SessionRow>(
            "SELECT * FROM ChatSessions ORDER BY UpdatedAt DESC");
        return rows.Select(x => x.ToModel()).ToList();
    }

    public async Task UpdateAsync(ChatSession session)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE ChatSessions SET Title=@Title,CharacterId=@CharacterId,IsGroupChat=@IsGroupChat,PersonaId=@PersonaId,LorebookId=@LorebookId," +
            "PromptPresetId=@PromptPresetId,AuthorNote=@AuthorNote,Summary=@Summary,UpdatedAt=@UpdatedAt WHERE Id=@Id",
            ToParameters(session));
    }

    public async Task DeleteAsync(long id)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM ChatSessions WHERE Id=@id", new { id });
    }

    public async Task<IReadOnlyList<long>> GetCharacterIdsAsync(long sessionId)
    {
        await using var connection = connectionFactory.CreateConnection();
        var ids = await connection.QueryAsync<long>(
            "SELECT CharacterId FROM ChatSessionCharacters WHERE ChatSessionId=@sessionId ORDER BY SortOrder,CharacterId",
            new { sessionId });
        return ids.ToList();
    }

    public async Task SetCharactersAsync(long sessionId, IReadOnlyCollection<long> characterIds)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM ChatSessionCharacters WHERE ChatSessionId=@sessionId",
            new { sessionId }, transaction);
        var order = 0;
        foreach (var characterId in characterIds.Distinct())
            await connection.ExecuteAsync(
                "INSERT INTO ChatSessionCharacters(ChatSessionId,CharacterId,SortOrder) VALUES(@sessionId,@characterId,@order)",
                new { sessionId, characterId, order = order++ }, transaction);
        await transaction.CommitAsync();
    }

    private static object ToParameters(ChatSession value) => new
    {
        value.Id, value.Title, value.CharacterId, IsGroupChat = value.IsGroupChat ? 1 : 0, value.PersonaId, value.LorebookId,
        value.PromptPresetId, value.AuthorNote, value.Summary,
        CreatedAt = value.CreatedAt.ToString("O"),
        UpdatedAt = value.UpdatedAt.ToString("O")
    };

    private sealed class SessionRow
    {
        public long Id { get; init; }
        public string Title { get; init; } = string.Empty;
        public long? CharacterId { get; init; }
        public int IsGroupChat { get; init; }
        public long? PersonaId { get; init; }
        public long? LorebookId { get; init; }
        public long? PromptPresetId { get; init; }
        public string AuthorNote { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
        public string UpdatedAt { get; init; } = string.Empty;
        public ChatSession ToModel() => new()
        {
            Id = Id, Title = Title, CharacterId = CharacterId, IsGroupChat = IsGroupChat != 0, PersonaId = PersonaId,
            LorebookId = LorebookId, PromptPresetId = PromptPresetId, AuthorNote = AuthorNote, Summary = Summary,
            CreatedAt = DateTimeOffset.Parse(CreatedAt),
            UpdatedAt = DateTimeOffset.Parse(UpdatedAt)
        };
    }
}

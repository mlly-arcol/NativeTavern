using Dapper;
using NativeTavern.Models;

namespace NativeTavern.Data.Repositories;

public sealed class ChatMessageRepository(DatabaseConnectionFactory connectionFactory)
{
    public async Task<long> AddAsync(ChatMessage message)
    {
        await using var connection = connectionFactory.CreateConnection();
        message.Id = await connection.ExecuteScalarAsync<long>(
            "INSERT INTO ChatMessages(ChatSessionId,Role,Content,CreatedAt,UpdatedAt,CurrentSwipeIndex) " +
            "VALUES(@ChatSessionId,@Role,@Content,@CreatedAt,@UpdatedAt,@CurrentSwipeIndex); SELECT last_insert_rowid();",
            ToParameters(message));
        return message.Id;
    }

    public async Task UpdateAsync(ChatMessage message)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "UPDATE ChatMessages SET Content=@Content,UpdatedAt=@UpdatedAt,CurrentSwipeIndex=@CurrentSwipeIndex WHERE Id=@Id",
            ToParameters(message));
    }

    public async Task<IReadOnlyList<ChatMessage>> GetBySessionAsync(long chatSessionId)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<MessageRow>(
            "SELECT * FROM ChatMessages WHERE ChatSessionId=@chatSessionId ORDER BY Id",
            new { chatSessionId });
        return rows.Select(x => x.ToModel()).ToList();
    }

    public async Task DeleteBySessionAsync(long chatSessionId)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM ChatMessages WHERE ChatSessionId=@chatSessionId", new { chatSessionId });
    }

    public async Task DeleteAsync(long id)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync("DELETE FROM ChatMessages WHERE Id=@id", new { id });
    }

    private static object ToParameters(ChatMessage value) => new
    {
        value.Id, value.ChatSessionId, Role = value.Role.ToString(), value.Content,
        CreatedAt = value.CreatedAt.ToString("O"),
        UpdatedAt = value.UpdatedAt?.ToString("O"), value.CurrentSwipeIndex
    };

    private sealed class MessageRow
    {
        public long Id { get; init; }
        public long ChatSessionId { get; init; }
        public string Role { get; init; } = string.Empty;
        public string Content { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
        public string? UpdatedAt { get; init; }
        public int CurrentSwipeIndex { get; init; }
        public ChatMessage ToModel() => new()
        {
            Id = Id, ChatSessionId = ChatSessionId,
            Role = Enum.Parse<ChatRole>(Role, true), Content = Content,
            CreatedAt = DateTimeOffset.Parse(CreatedAt),
            UpdatedAt = string.IsNullOrEmpty(UpdatedAt) ? null : DateTimeOffset.Parse(UpdatedAt),
            CurrentSwipeIndex = CurrentSwipeIndex
        };
    }
}

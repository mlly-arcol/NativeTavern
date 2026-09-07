using Dapper;
using NativeTavern.Models;

namespace NativeTavern.Data.Repositories;

public sealed class MessageSwipeRepository(DatabaseConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<MessageSwipe>> GetByMessageAsync(long messageId)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SwipeRow>(
            "SELECT * FROM MessageSwipes WHERE ChatMessageId=@messageId ORDER BY SwipeIndex",
            new { messageId });
        return rows.Select(x => x.ToModel()).ToList();
    }

    public async Task AddAsync(MessageSwipe swipe)
    {
        await using var connection = connectionFactory.CreateConnection();
        swipe.Id = await connection.ExecuteScalarAsync<long>(
            "INSERT INTO MessageSwipes(ChatMessageId,SwipeIndex,Content,CreatedAt) " +
            "VALUES(@ChatMessageId,@SwipeIndex,@Content,@CreatedAt); SELECT last_insert_rowid();",
            new
            {
                swipe.ChatMessageId, swipe.SwipeIndex, swipe.Content,
                CreatedAt = swipe.CreatedAt.ToString("O")
            });
    }

    public async Task ReplaceWithSingleAsync(long messageId, string content)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync(
            "DELETE FROM MessageSwipes WHERE ChatMessageId=@messageId",
            new { messageId }, transaction);
        await connection.ExecuteAsync(
            "INSERT INTO MessageSwipes(ChatMessageId,SwipeIndex,Content,CreatedAt) VALUES(@messageId,0,@content,@createdAt)",
            new { messageId, content, createdAt = DateTimeOffset.UtcNow.ToString("O") }, transaction);
        await transaction.CommitAsync();
    }

    private sealed class SwipeRow
    {
        public long Id { get; init; }
        public long ChatMessageId { get; init; }
        public int SwipeIndex { get; init; }
        public string Content { get; init; } = string.Empty;
        public string CreatedAt { get; init; } = string.Empty;
        public MessageSwipe ToModel() => new()
        {
            Id = Id,
            ChatMessageId = ChatMessageId,
            SwipeIndex = SwipeIndex,
            Content = Content,
            CreatedAt = DateTimeOffset.Parse(CreatedAt)
        };
    }
}

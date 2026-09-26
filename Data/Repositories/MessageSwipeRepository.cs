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

    /// Every assistant reply needs a swipe 0 holding its own text, and filling those in one message at
    /// a time is what made opening a long conversation crawl.
    public async Task BackfillFirstSwipesAsync(long sessionId)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT INTO MessageSwipes(ChatMessageId,SwipeIndex,Content,CreatedAt) " +
            "SELECT m.Id,0,m.Content,m.CreatedAt FROM ChatMessages m " +
            "WHERE m.ChatSessionId=@sessionId AND m.Role='Assistant' AND m.Content<>'' " +
            "AND NOT EXISTS (SELECT 1 FROM MessageSwipes s WHERE s.ChatMessageId=m.Id)",
            new { sessionId });
    }

    public async Task<IReadOnlyDictionary<long, int>> GetCountsBySessionAsync(long sessionId)
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<CountRow>(
            "SELECT ChatMessageId, COUNT(*) AS Total FROM MessageSwipes " +
            "WHERE ChatMessageId IN (SELECT Id FROM ChatMessages WHERE ChatSessionId=@sessionId) " +
            "GROUP BY ChatMessageId", new { sessionId });
        return rows.ToDictionary(x => x.ChatMessageId, x => x.Total);
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

    private sealed class CountRow
    {
        public long ChatMessageId { get; init; }
        public int Total { get; init; }
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

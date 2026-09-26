using System.Text.Json;
using Dapper;
using NativeTavern.Helpers;
using NativeTavern.Models;

namespace NativeTavern.Data.Repositories;

/// Unsent composer text, one row per conversation. Rows are tiny and few, so the whole table is read
/// once at startup and rewritten only when a draft actually changes.
public sealed class ComposerDraftRepository(DatabaseConnectionFactory connectionFactory)
{
    public async Task<IReadOnlyList<ComposerDraft>> GetAllAsync()
    {
        await using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<Row>(
            "SELECT ChatSessionId,DraftText,DraftImages FROM ComposerDrafts");
        return rows.Select(x => x.ToModel()).ToList();
    }

    /// An empty composer is stored as no row at all, which keeps the table free of stale noise.
    public async Task SaveAsync(long chatSessionId, string text, IReadOnlyList<string> images)
    {
        await using var connection = connectionFactory.CreateConnection();
        if (string.IsNullOrEmpty(text) && images.Count == 0)
        {
            await connection.ExecuteAsync(
                "DELETE FROM ComposerDrafts WHERE ChatSessionId=@chatSessionId", new { chatSessionId });
            return;
        }
        await connection.ExecuteAsync(
            "INSERT INTO ComposerDrafts(ChatSessionId,DraftText,DraftImages,UpdatedAt) VALUES(@chatSessionId,@text,@images,@now) " +
            "ON CONFLICT(ChatSessionId) DO UPDATE SET DraftText=excluded.DraftText,DraftImages=excluded.DraftImages,UpdatedAt=excluded.UpdatedAt",
            new
            {
                chatSessionId,
                text,
                images = JsonSerializer.Serialize(images, JsonDefaults.Options),
                now = DateTimeOffset.UtcNow.ToString("O")
            });
    }

    public async Task DeleteAsync(long chatSessionId)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "DELETE FROM ComposerDrafts WHERE ChatSessionId=@chatSessionId", new { chatSessionId });
    }

    private sealed class Row
    {
        public long ChatSessionId { get; init; }
        public string DraftText { get; init; } = string.Empty;
        public string DraftImages { get; init; } = "[]";

        public ComposerDraft ToModel() => new(ChatSessionId, DraftText, ReadImages(DraftImages));

        private static IReadOnlyList<string> ReadImages(string json)
        {
            try { return JsonSerializer.Deserialize<string[]>(json, JsonDefaults.Options) ?? []; }
            catch (JsonException) { return []; }
        }
    }
}

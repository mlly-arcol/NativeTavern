using System.Text.Json;
using Dapper;
using NativeTavern.Helpers;
using NativeTavern.Models;

namespace NativeTavern.Data.Repositories;

public sealed class CharacterStatusRepository(DatabaseConnectionFactory connectionFactory)
{
    public async Task<CharacterStatusSnapshot?> GetAsync(long chatSessionId, long characterId)
    {
        await using var connection = connectionFactory.CreateConnection();
        var row = await connection.QuerySingleOrDefaultAsync<Row>(
            "SELECT * FROM CharacterStatuses WHERE ChatSessionId=@chatSessionId AND CharacterId=@characterId",
            new { chatSessionId, characterId });
        return row?.ToModel();
    }

    public async Task UpsertAsync(CharacterStatusSnapshot value)
    {
        await using var connection = connectionFactory.CreateConnection();
        await connection.ExecuteAsync(
            "INSERT INTO CharacterStatuses(ChatSessionId,CharacterId,CharacterName,Summary,AttributesJson,SourceMessageId,UpdatedAt) " +
            "VALUES(@ChatSessionId,@CharacterId,@CharacterName,@Summary,@AttributesJson,@SourceMessageId,@UpdatedAt) " +
            "ON CONFLICT(ChatSessionId,CharacterId) DO UPDATE SET CharacterName=excluded.CharacterName," +
            "Summary=excluded.Summary,AttributesJson=excluded.AttributesJson,SourceMessageId=excluded.SourceMessageId,UpdatedAt=excluded.UpdatedAt",
            new
            {
                value.ChatSessionId, value.CharacterId, value.CharacterName, value.Summary,
                AttributesJson = JsonSerializer.Serialize(value.Attributes, JsonDefaults.Options),
                value.SourceMessageId, UpdatedAt = value.UpdatedAt.ToString("O")
            });
    }

    private sealed class Row
    {
        public long ChatSessionId { get; init; }
        public long CharacterId { get; init; }
        public string CharacterName { get; init; } = string.Empty;
        public string Summary { get; init; } = string.Empty;
        public string AttributesJson { get; init; } = "[]";
        public long? SourceMessageId { get; init; }
        public string UpdatedAt { get; init; } = string.Empty;

        public CharacterStatusSnapshot ToModel() => new()
        {
            ChatSessionId = ChatSessionId,
            CharacterId = CharacterId,
            CharacterName = CharacterName,
            Summary = Summary,
            Attributes = JsonSerializer.Deserialize<List<CharacterStatusAttribute>>(AttributesJson, JsonDefaults.Options) ?? [],
            SourceMessageId = SourceMessageId,
            UpdatedAt = DateTimeOffset.Parse(UpdatedAt)
        };
    }
}

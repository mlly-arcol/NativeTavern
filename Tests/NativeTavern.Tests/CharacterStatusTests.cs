using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NativeTavern.Data;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

public sealed class CharacterStatusTests
{
    [Fact]
    public void ParsesJsonWrappedInModelCommentaryAndDeduplicatesAttributes()
    {
        var character = new Character { Id = 7, Name = "琳" };

        var result = CharacterStatusService.ParseResponse(
            "状态如下：```json\n{\"summary\":\"警觉地观察四周\",\"attributes\":[" +
            "{\"name\":\"情绪\",\"value\":\"警觉\",\"description\":\"听见脚步声\"}," +
            "{\"name\":\"情绪\",\"value\":\"重复项\"}," +
            "{\"name\":\"魔力\",\"value\":\"72/100\"}]}\n```",
            3, character, 11);

        Assert.Equal("琳", result.CharacterName);
        Assert.Equal(2, result.Attributes.Count);
        Assert.Equal("警觉", result.Attributes[0].Value);
        Assert.Equal(11, result.SourceMessageId);
    }

    [Fact]
    public async Task RepositoryKeepsStatusesSeparatePerConversationAndCharacter()
    {
        var directory = Path.Combine(Path.GetTempPath(), "NativeTavernStatusTest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var database = Path.Combine(directory, "status.db");
        try
        {
            var factory = new DatabaseConnectionFactory(database);
            await new DatabaseInitializer(factory, NullLogger<DatabaseInitializer>.Instance).InitializeAsync();
            var characters = new CharacterRepository(factory);
            var sessions = new ChatSessionRepository(factory);
            var statuses = new CharacterStatusRepository(factory);
            var now = DateTimeOffset.UtcNow;
            var character = new Character { Name = "琳", CreatedAt = now, UpdatedAt = now };
            await characters.CreateAsync(character);
            var first = new ChatSession { Title = "剧情一", CharacterId = character.Id, CreatedAt = now, UpdatedAt = now };
            var second = new ChatSession { Title = "剧情二", CharacterId = character.Id, CreatedAt = now, UpdatedAt = now };
            await sessions.CreateAsync(first);
            await sessions.CreateAsync(second);

            await statuses.UpsertAsync(new CharacterStatusSnapshot
            {
                ChatSessionId = first.Id, CharacterId = character.Id, CharacterName = character.Name,
                Summary = "疲惫", Attributes = [new CharacterStatusAttribute { Name = "体力", Value = "20/100" }], UpdatedAt = now
            });

            Assert.Equal("20/100", Assert.Single((await statuses.GetAsync(first.Id, character.Id))!.Attributes).Value);
            Assert.Null(await statuses.GetAsync(second.Id, character.Id));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(directory, true);
        }
    }
}

using Microsoft.Data.Sqlite;
using NativeTavern.Data;
using NativeTavern.Data.Repositories;
using NativeTavern.Importers;
using NativeTavern.Models;
using Microsoft.Extensions.Logging.Abstractions;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

/// <summary>
/// "@名字 关键词" is how a writer finds one character's lines across a whole library, so the speaker
/// token has to resolve through both the message's own speaker and the conversation's character.
/// </summary>
public class SpeakerSearchTests : IDisposable
{
    private readonly string _directory;
    private readonly ChatMessageRepository _messages;
    private readonly CharacterRepository _characters;
    private long _soloSessionId;
    private long _groupId;
    private long _yunId;
    private long _redId;

    public SpeakerSearchTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "NativeTavernSpeaker-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        var factory = new DatabaseConnectionFactory(Path.Combine(_directory, "data.db"));
        new DatabaseInitializer(factory, NullLogger<DatabaseInitializer>.Instance).InitializeAsync().GetAwaiter().GetResult();
        _messages = new ChatMessageRepository(factory);
        _characters = new CharacterRepository(factory);
        var sessions = new ChatSessionRepository(factory);
        var now = DateTimeOffset.UtcNow;

        _yunId = CreateCharacter(factory, "云梦泽", "守墓人, 阿泽", now);
        _redId = CreateCharacter(factory, "红衣客", "", now);
        _soloSessionId = sessions.CreateAsync(new ChatSession
        {
            Title = "界碑之外", CharacterId = _yunId, CreatedAt = now, UpdatedAt = now
        }).GetAwaiter().GetResult();
        _groupId = sessions.CreateAsync(new ChatSession
        {
            Title = "群像", IsGroupChat = true, CreatedAt = now, UpdatedAt = now
        }).GetAwaiter().GetResult();
        sessions.SetCharactersAsync(_groupId, [_yunId, _redId]).GetAwaiter().GetResult();

        Add(_soloSessionId, ChatRole.User, null, "剑何在");
        Add(_soloSessionId, ChatRole.Assistant, null, "剑在界碑下，谁都不许碰。");
        Add(_groupId, ChatRole.User, null, "你们是谁？");
        Add(_groupId, ChatRole.Assistant, _yunId, "守墓人。");
        Add(_groupId, ChatRole.Assistant, _redId, "红衣客，来收账的。");
    }

    [Theory]
    [InlineData("@云梦泽 剑", "剑", "云梦泽")]
    [InlineData("  @阿泽   剑 ", "剑", "阿泽")]
    [InlineData("@云梦泽", "", "云梦泽")]
    [InlineData("剑何在", "剑何在", "")]
    [InlineData("a@b 剑", "a@b 剑", "")]
    public void SpeakerTokenIsSplitFromTheSearchTerm(string query, string term, string speaker)
    {
        var parsed = ChatMessageRepository.SplitSpeakerFilter(query);

        Assert.Equal(term, parsed.Term);
        Assert.Equal(speaker, parsed.Speaker);
    }

    [Fact]
    public async Task SpeakerAloneListsEveryLineThatCharacterSaid()
    {
        var results = await _messages.SearchAsync("@云梦泽");

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.NotEqual(ChatRole.User, result.Role));
        Assert.Contains(results, result => result.Speaker == "云梦泽");
    }

    [Fact]
    public async Task AliasesResolveToTheSameCharacter()
    {
        var byName = await _messages.SearchAsync("@云梦泽");
        var byAlias = await _messages.SearchAsync("@阿泽");

        Assert.Equal(byName.Select(x => x.MessageId).OrderBy(x => x), byAlias.Select(x => x.MessageId).OrderBy(x => x));
    }

    [Fact]
    public async Task SpeakerAndKeywordCombine()
    {
        var results = await _messages.SearchAsync("@红衣客 收账");

        var hit = Assert.Single(results);
        Assert.Equal("群像", hit.SessionTitle);
        Assert.Equal("红衣客", hit.Speaker);
        Assert.Equal(ChatRole.Assistant, hit.Role);
        Assert.Contains("收账", hit.Snippet, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UnknownSpeakersAndUserTurnsAreNotMatched()
    {
        Assert.Empty(await _messages.SearchAsync("@路人 剑"));
        Assert.Empty(await _messages.SearchAsync("@云梦泽 何在"));
    }

    [Fact]
    public async Task AliasesAreSearchableOnTheCharacterItself()
    {
        var found = await _characters.SearchAsync("阿泽", false);

        Assert.Equal("云梦泽", Assert.Single(found).Name);
    }

    [Fact]
    public void AliasesTravelWithTheCharacterCard()
    {
        var json = DataExportService.BuildCharacterCardJson(new Character { Name = "云梦泽", Aliases = "守墓人, 阿泽" });

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var data = document.RootElement.GetProperty("data");
        Assert.Equal("守墓人", data.GetProperty("nickname").GetString());
        Assert.Equal(
            new[] { "守墓人", "阿泽" },
            data.GetProperty("alt_names").EnumerateArray().Select(x => x.GetString()));
        Assert.DoesNotContain("\\u", json, StringComparison.Ordinal);

        Assert.Equal("守墓人, 阿泽", new CharacterCardImporter().ParseJson(json).Aliases);

        // A card authored elsewhere can repeat names; the alias list collapses them.
        var repeated = new CharacterCardImporter().ParseJson(json.Replace("\"阿泽\"]", "\"阿泽\", \"阿泽\"]"));
        Assert.Equal(2, repeated.AliasList.Count);
    }

    private long CreateCharacter(
        DatabaseConnectionFactory factory, string name, string aliases, DateTimeOffset now)
    {
        var repository = new CharacterRepository(factory);
        var character = new Character
        {
            Name = name, Aliases = aliases, Description = name, Personality = "", Scenario = "",
            FirstMessage = "", ExampleMessages = "", Creator = "", Tags = "", AvatarPath = "",
            CreatedAt = now, UpdatedAt = now
        };
        return repository.CreateAsync(character).GetAwaiter().GetResult();
    }

    private void Add(long sessionId, ChatRole role, long? speaker, string content) =>
        _messages.AddAsync(new ChatMessage
        {
            ChatSessionId = sessionId, Role = role, SpeakerCharacterId = speaker, Content = content,
            CreatedAt = DateTimeOffset.UtcNow
        }).GetAwaiter().GetResult();

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }
}

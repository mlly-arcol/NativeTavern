using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NativeTavern.Data;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

/// <summary>Covers the two new data-backed features: cross-conversation search and stored regex scripts.</summary>
public class MessageSearchAndScriptsTests : IDisposable
{
    private readonly string _directory;
    private readonly DatabaseConnectionFactory _factory;

    public MessageSearchAndScriptsTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "NativeTavernSearch-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _factory = new DatabaseConnectionFactory(Path.Combine(_directory, "data.db"));
        new DatabaseInitializer(_factory, NullLogger<DatabaseInitializer>.Instance).InitializeAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }

    private async Task<ChatMessage> AddAsync(long sessionId, ChatRole role, string content)
    {
        var messages = new ChatMessageRepository(_factory);
        var message = new ChatMessage
        {
            ChatSessionId = sessionId, Role = role, Content = content, CreatedAt = DateTimeOffset.UtcNow,
        };
        await messages.AddAsync(message);
        return message;
    }

    private async Task<long> NewSessionAsync(string title)
    {
        var sessions = new ChatSessionRepository(_factory);
        var session = new ChatSession { Title = title, CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        await sessions.CreateAsync(session);
        return session.Id;
    }

    [Fact]
    public async Task SearchFindsMessagesAcrossConversationsAndBuildsASnippet()
    {
        var first = await NewSessionAsync("山门试炼");
        var second = await NewSessionAsync("丹药纠纷");
        await AddAsync(first, ChatRole.Assistant, "开篇文字。" + new string('字', 200) + " 她低声说：阵法将破。" + new string('！', 200));
        await AddAsync(second, ChatRole.User, "完全无关的内容");

        var results = await new ChatMessageRepository(_factory).SearchAsync("阵法将破");
        var hit = Assert.Single(results);
        Assert.Equal(first, hit.ChatSessionId);
        Assert.Equal("山门试炼", hit.SessionTitle);
        Assert.Contains("阵法将破", hit.Snippet);
        Assert.True(hit.Snippet.Length < 200, "the snippet should stay short");
    }

    [Theory]
    [InlineData("%", "100% coverage")]
    [InlineData("_", "a_b")]
    public async Task WildcardsInQueriesMatchLiterally(string term, string content)
    {
        var session = await NewSessionAsync("Escapes");
        await AddAsync(session, ChatRole.User, content);
        await AddAsync(session, ChatRole.User, "完全无关的内容");

        var results = await new ChatMessageRepository(_factory).SearchAsync(term);
        Assert.Equal(content, Assert.Single(results).Snippet.Trim('…'));
    }

    [Fact]
    public async Task EmptyQueriesReturnNothingWithoutTouchingTheDatabase()
    {
        Assert.Empty(await new ChatMessageRepository(_factory).SearchAsync("   "));
        Assert.Empty(await new ChatMessageRepository(_factory).SearchAsync("x", limit: 0));
    }

    [Fact]
    public async Task ServiceAppliesEnabledScriptsAndReloadsAfterChanges()
    {
        var repository = new RegexScriptRepository(_factory);
        var service = new RegexScriptService(repository, NullLogger<RegexScriptService>.Instance);
        await service.SaveAsync(new RegexScript
        {
            Name = "旁白", Pattern = @"（([^）]*)）", Replacement = "[$1]",
            Target = RegexScriptTarget.AssistantOutput, Mode = RegexScriptMode.Regex,
        });

        Assert.Equal("她低声说：[阵法将破]",
            await service.ApplyAsync("她低声说：（阵法将破）", RegexScriptTarget.AssistantOutput));
        Assert.Equal("她低声说：（阵法将破）",
            await service.ApplyAsync("她低声说：（阵法将破）", RegexScriptTarget.UserInput));

        var stored = Assert.Single(await service.GetScriptsAsync());
        stored.IsEnabled = false;
        await service.SaveAsync(stored);
        Assert.Equal("她低声说：（阵法将破）",
            await service.ApplyAsync("她低声说：（阵法将破）", RegexScriptTarget.AssistantOutput));
    }

    [Fact]
    public async Task InvalidStoredPatternDoesNotBreakReplacement()
    {
        var service = new RegexScriptService(
            new RegexScriptRepository(_factory), NullLogger<RegexScriptService>.Instance);
        await service.SaveAsync(new RegexScript
        {
            Name = "broken", Pattern = "([unclosed", Replacement = "x",
            Target = RegexScriptTarget.AssistantOutput, Mode = RegexScriptMode.Regex,
        });

        Assert.Equal("keep me", await service.ApplyAsync("keep me", RegexScriptTarget.AssistantOutput));
    }
}

using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NativeTavern.Data;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;
using NativeTavern.Services;
using NativeTavern.ViewModels;
using Xunit;

namespace NativeTavern.Tests;

/// An unsent message is the one thing a writer would least like to lose to a closed window, so the
/// draft has to live in the database and land back on the conversation it was written for.
public class ComposerDraftTests : IDisposable
{
    private readonly string _directory;
    private readonly DatabaseConnectionFactory _factory;
    private readonly ChatSessionRepository _sessions;
    private readonly ComposerDraftRepository _drafts;

    public ComposerDraftTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "NativeTavernDrafts-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _factory = new DatabaseConnectionFactory(Path.Combine(_directory, "data.db"));
        new DatabaseInitializer(_factory, NullLogger<DatabaseInitializer>.Instance)
            .InitializeAsync().GetAwaiter().GetResult();
        _sessions = new ChatSessionRepository(_factory);
        _drafts = new ComposerDraftRepository(_factory);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }

    [Fact]
    public async Task DraftsComeBackWithTheirImages()
    {
        var session = await CreateSessionAsync("夜访");
        await _drafts.SaveAsync(session.Id, "山门外的灯还亮着。", ["a.png", "b.png"]);

        var stored = Assert.Single(await _drafts.GetAllAsync());
        Assert.Equal(session.Id, stored.ChatSessionId);
        Assert.Equal("山门外的灯还亮着。", stored.Text);
        Assert.Equal(new[] { "a.png", "b.png" }, stored.Images);
    }

    [Fact]
    public async Task DraftTextIsStoredReadable()
    {
        var session = await CreateSessionAsync("可读性");
        await _drafts.SaveAsync(session.Id, "守墓人", []);

        await using var connection = _factory.CreateConnection();
        var raw = await connection.QuerySingleAsync<string>(
            "SELECT DraftText FROM ComposerDrafts WHERE ChatSessionId=@id", new { id = session.Id });
        Assert.Contains("守墓人", raw);
        Assert.DoesNotContain("\\u", raw);
    }

    [Fact]
    public async Task SavingTwiceReplacesTheDraftInsteadOfStackingRows()
    {
        var session = await CreateSessionAsync("改写");
        await _drafts.SaveAsync(session.Id, "第一版", ["a.png"]);
        await _drafts.SaveAsync(session.Id, "第二版", []);

        var stored = Assert.Single(await _drafts.GetAllAsync());
        Assert.Equal("第二版", stored.Text);
        Assert.Empty(stored.Images);
    }

    [Fact]
    public async Task SendingTheMessageLeavesNoRowBehind()
    {
        var session = await CreateSessionAsync("发出");
        await _drafts.SaveAsync(session.Id, "待发内容", ["a.png"]);

        await _drafts.SaveAsync(session.Id, string.Empty, []);
        Assert.Empty(await _drafts.GetAllAsync());
    }

    [Fact]
    public async Task DeletingAConversationTakesItsDraftWithIt()
    {
        var session = await CreateSessionAsync("删除");
        await _drafts.SaveAsync(session.Id, "随会话消失", []);

        await _sessions.DeleteAsync(session.Id);
        Assert.Empty(await _drafts.GetAllAsync());
    }

    [Fact]
    public async Task AfterARestartEachConversationGetsBackItsOwnDraft()
    {
        var first = await CreateSessionAsync("甲对话");
        var second = await CreateSessionAsync("乙对话");
        await _drafts.SaveAsync(first.Id, "甲还没发出去", []);

        var store = new DraftStore();
        foreach (var draft in await _drafts.GetAllAsync())
            store.Remember(draft.ChatSessionId, DraftStore.Capture(draft.Text, draft.Images));

        var empty = DraftStore.Capture(string.Empty, []);
        Assert.Equal("甲还没发出去", store.OnLoadFinished(first.Id, empty, empty).Text);
        Assert.Equal(string.Empty, store.OnLoadFinished(second.Id, empty, empty).Text);
    }

    private async Task<ChatSession> CreateSessionAsync(string title)
    {
        var stamp = DateTimeOffset.UtcNow;
        var session = new ChatSession { Title = title, CreatedAt = stamp, UpdatedAt = stamp };
        session.Id = await _sessions.CreateAsync(session);
        return session;
    }
}

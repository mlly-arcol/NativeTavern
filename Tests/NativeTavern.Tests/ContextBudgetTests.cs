using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NativeTavern.Data;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

/// <summary>
/// A long roleplay used to be sent whole, so the provider answered with a context-length error.
/// The newest turns are the ones a reader cannot live without, so they are what survives trimming.
/// </summary>
public sealed class ContextBudgetTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "nt-budget-" + Guid.NewGuid().ToString("N"));
    private PromptService? _prompts;

    /// <summary>xUnit builds one fixture per test, so each one gets a private database.</summary>
    private async Task<PromptService> PromptsAsync()
    {
        if (_prompts is not null) return _prompts;
        Directory.CreateDirectory(_directory);
        var factory = new DatabaseConnectionFactory(Path.Combine(_directory, "data.db"));
        await new DatabaseInitializer(factory, NullLogger<DatabaseInitializer>.Instance).InitializeAsync();
        _prompts = new PromptService(
            new PromptRepository(factory),
            new CharacterRepository(factory),
            new ChatSessionRepository(factory),
            new KnowledgeService(new KnowledgeRepository(factory), NullLogger<KnowledgeService>.Instance));
        return _prompts;
    }

    [Fact]
    public async Task BuildDropsTheOldestTurnsAndReportsIt()
    {
        var history = History(30);
        var lastTurn = history[^1].Content;

        var result = await (await PromptsAsync()).BuildAsync(new ChatSession(), history, Settings(4000));

        Assert.True(result.TrimmedMessages > 0, "a 9 000 token history should not fit a 4 000 window");
        Assert.Equal(30 - result.TrimmedMessages, result.Messages.Count(message => message.Role != "system"));
        Assert.Contains(lastTurn, string.Join("\n", result.Messages.Select(message => message.Content)));
    }

    [Fact]
    public async Task TurningTheBudgetOffSendsEveryTurn()
    {
        var result = await (await PromptsAsync()).BuildAsync(new ChatSession(), History(30), Settings(4000, trim: false));

        Assert.Equal(0, result.TrimmedMessages);
        Assert.Equal(30, result.Messages.Count(message => message.Role != "system"));
    }

    [Fact]
    public async Task ShortChatsAreNeverCut()
    {
        var result = await (await PromptsAsync()).BuildAsync(new ChatSession(), History(4), Settings(8192));

        Assert.Equal(0, result.TrimmedMessages);
        Assert.Equal(4, result.Messages.Count);
    }

    [Fact]
    public void TinyBudgetsStillKeepTheLastTwoTurns()
    {
        var history = History(10);

        Assert.Equal(8, PromptService.TrimHistoryToBudget(history, Settings(64), 512, 0));
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void TwoTurnHistoriesNeedNoTrimmingEvenWhenTheyOverflow()
    {
        var history = History(2);
        Assert.Equal(0, PromptService.TrimHistoryToBudget(history, Settings(64), 512, 0));
        Assert.Equal(2, history.Count);
    }

    [Fact]
    public void UnconfiguredContextWindowsDisableTrimming()
    {
        var history = History(10);
        Assert.Equal(0, PromptService.TrimHistoryToBudget(history, Settings(0), 512, 0));
        Assert.Equal(10, history.Count);
    }

    private static List<ChatMessage> History(int count) => Enumerable.Range(0, count)
        .Select(index => new ChatMessage
        {
            Role = index % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
            Content = new string('记', 300) + index
        })
        .ToList();

    private static ProviderSettings Settings(int contextLength, bool trim = true) => new()
    {
        BaseUrl = "http://127.0.0.1:8080/v1",
        Model = "test-model",
        ContextLength = contextLength,
        MaxTokens = 512,
        TrimHistoryToContext = trim
    };

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }
}

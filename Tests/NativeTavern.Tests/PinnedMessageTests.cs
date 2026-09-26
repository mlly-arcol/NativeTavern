using Dapper;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using NativeTavern.Data;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

/// <summary>Covers the pinned-message flag: storage, the added-column migration and export marking.</summary>
public class PinnedMessageTests : IDisposable
{
    private readonly string _directory;
    private readonly DatabaseConnectionFactory _factory;
    private readonly ChatMessageRepository _messages;
    private readonly long _sessionId;

    public PinnedMessageTests()
    {
        _directory = Path.Combine(Path.GetTempPath(), "NativeTavernPins-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_directory);
        _factory = new DatabaseConnectionFactory(Path.Combine(_directory, "data.db"));
        new DatabaseInitializer(_factory, NullLogger<DatabaseInitializer>.Instance)
            .InitializeAsync().GetAwaiter().GetResult();
        _messages = new ChatMessageRepository(_factory);
        _sessionId = new ChatSessionRepository(_factory)
            .CreateAsync(new ChatSession { Title = "Pins", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow })
            .GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }

    private async Task<ChatMessage> AddAsync(string content)
    {
        var message = new ChatMessage
        {
            ChatSessionId = _sessionId, Role = ChatRole.Assistant, Content = content, CreatedAt = DateTimeOffset.UtcNow,
        };
        await _messages.AddAsync(message);
        return message;
    }

    [Fact]
    public async Task PinSurvivesAReload()
    {
        var message = await AddAsync("keeper");
        Assert.False(message.IsPinned);

        await _messages.SetPinnedAsync(message.Id, true);

        var reloaded = Assert.Single(await _messages.GetBySessionAsync(_sessionId));
        Assert.True(reloaded.IsPinned);
        Assert.Equal("keeper", reloaded.Content);
    }

    [Fact]
    public async Task PinningLeavesContentAndTimestampAlone()
    {
        var created = DateTimeOffset.UtcNow.AddMinutes(-30);
        var message = new ChatMessage
        {
            ChatSessionId = _sessionId, Role = ChatRole.Assistant, Content = "keep me",
            CreatedAt = created, UpdatedAt = created, IsPinned = true,
        };
        await _messages.AddAsync(message);

        await _messages.SetPinnedAsync(message.Id, false);

        var reloaded = Assert.Single(await _messages.GetBySessionAsync(_sessionId));
        Assert.False(reloaded.IsPinned);
        Assert.Equal("keep me", reloaded.Content);
        Assert.Equal(created, reloaded.UpdatedAt);
    }

    [Fact]
    public async Task InitializerRestoresTheColumnForAnOlderDatabase()
    {
        var path = Path.Combine(_directory, "older.db");
        var factory = new DatabaseConnectionFactory(path);
        var initializer = new DatabaseInitializer(factory, NullLogger<DatabaseInitializer>.Instance);
        await initializer.InitializeAsync();
        var sessionId = await new ChatSessionRepository(factory).CreateAsync(
            new ChatSession { Title = "Old", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
        var messages = new ChatMessageRepository(factory);
        await messages.AddAsync(new ChatMessage
        {
            ChatSessionId = sessionId, Role = ChatRole.User, Content = "written before the flag existed",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        await using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            await connection.OpenAsync();
            await connection.ExecuteAsync("ALTER TABLE ChatMessages DROP COLUMN IsPinned");
        }
        SqliteConnection.ClearAllPools();

        await initializer.InitializeAsync();

        var reloaded = Assert.Single(await messages.GetBySessionAsync(sessionId));
        Assert.False(reloaded.IsPinned);
        Assert.Equal("written before the flag existed", reloaded.Content);
    }

    [Fact]
    public void ConversationExportMarksPinnedMessages()
    {
        var session = new ChatSession { Title = "Chapter", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };
        var keep = new ChatMessage { Role = ChatRole.Assistant, Content = "the good line", IsPinned = true };
        var plain = new ChatMessage { Role = ChatRole.Assistant, Content = "filler" };

        var markdown = DataExportService.BuildConversationMarkdown(session, [keep, plain]);
        var json = DataExportService.BuildConversationJson(session, [keep, plain]);

        Assert.Contains("★ pinned", markdown);
        Assert.Single(markdown.Split('\n'), line => line.StartsWith("## ") && line.Contains("★"));
        Assert.Contains("\"pinned\":true", json);
        Assert.DoesNotContain("\"pinned\":false", json);
    }
}

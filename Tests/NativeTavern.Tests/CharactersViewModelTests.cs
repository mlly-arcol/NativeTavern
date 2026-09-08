using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using NativeTavern.Models;
using NativeTavern.Services;
using NativeTavern.ViewModels;
using Xunit;

namespace NativeTavern.Tests;

public sealed class CharactersViewModelTests
{
    [Fact]
    public async Task InitializeAsync_SelectsFirstCharacter()
    {
        var service = new ControlledCharacterService();
        var logger = new RecordingLogger<CharactersViewModel>();
        var viewModel = new CharactersViewModel(service, logger);

        var refresh = viewModel.InitializeAsync();
        service.Complete(string.Empty, Character(1, "First"), Character(2, "Second"));
        await refresh;

        Assert.Equal(2, viewModel.Characters.Count);
        Assert.Equal(1, viewModel.SelectedCharacter?.Id);
        Assert.Equal("First", viewModel.Name);
    }

    [Fact]
    public async Task SearchRefresh_LatestRequestWinsWhenOlderRequestFinishesLast()
    {
        var service = new ControlledCharacterService();
        var logger = new RecordingLogger<CharactersViewModel>();
        var viewModel = new CharactersViewModel(service, logger);

        viewModel.SearchText = "old";
        viewModel.SearchText = "new";

        service.Complete("new", Character(2, "New result"));
        await WaitUntilAsync(() => viewModel.SelectedCharacter?.Id == 2);

        service.Complete("old", Character(1, "Stale result"));
        await WaitUntilAsync(() => logger.Messages.Any(x => x.Contains("Discarded stale character refresh")));

        Assert.Single(viewModel.Characters);
        Assert.Equal(2, viewModel.SelectedCharacter?.Id);
    }

    [Fact]
    public async Task RefreshFailure_IsLoggedAndShownToUser()
    {
        var service = new ControlledCharacterService();
        var logger = new RecordingLogger<CharactersViewModel>();
        var viewModel = new CharactersViewModel(service, logger);

        var refresh = viewModel.InitializeAsync();
        service.Fail(string.Empty, new InvalidOperationException("database unavailable"));
        await refresh;

        Assert.Equal("角色加载失败，请查看日志。", viewModel.StatusMessage);
        Assert.Contains(logger.Entries, x =>
            x.Level == LogLevel.Error &&
            x.Message.Contains("Character refresh") &&
            x.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task InitializeAsync_BuildsCollapsedGroupTreeWithoutDuplicatingMembers()
    {
        var service = new ControlledCharacterService();
        service.Groups.Add(new CharacterGroup { Name = "Story", CharacterCount = 2 });
        var viewModel = new CharactersViewModel(service, new RecordingLogger<CharactersViewModel>());

        var refresh = viewModel.InitializeAsync();
        service.Complete(
            string.Empty,
            Character(1, "One", "Story"),
            Character(2, "Two", "story"),
            Character(3, "Three"));
        await refresh;

        var group = Assert.Single(viewModel.CharacterGroups);
        Assert.Equal("Story", group.Name);
        Assert.Equal(2, group.CharacterCount);
        Assert.Equal(2, group.Characters.Count);
        Assert.False(group.IsExpanded);
        Assert.Single(viewModel.UngroupedCharacters);
        Assert.All(group.Characters, character =>
            Assert.Equal("Story", character.GroupName, ignoreCase: true));
        Assert.Equal(1, service.SearchCount);
    }

    [Fact]
    public async Task Search_HidesGroupsWithoutMatchingCharacters()
    {
        var service = new ControlledCharacterService();
        service.Groups.Add(new CharacterGroup { Name = "Story", CharacterCount = 2 });
        service.Groups.Add(new CharacterGroup { Name = "Empty", CharacterCount = 0 });
        var viewModel = new CharactersViewModel(service, new RecordingLogger<CharactersViewModel>());

        viewModel.SearchText = "hero";
        service.Complete("hero", Character(1, "Hero", "Story"));
        await WaitUntilAsync(() => viewModel.SelectedCharacter?.Id == 1);

        Assert.Equal("Story", Assert.Single(viewModel.CharacterGroups).Name);
        Assert.Empty(viewModel.UngroupedCharacters);
    }

    private static Character Character(long id, string name, string groupName = "") => new()
    {
        Id = id,
        Name = name,
        GroupName = groupName,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow
    };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!condition())
        {
            await Task.Delay(10, timeout.Token);
        }
    }

    private sealed class ControlledCharacterService : ICharacterService
    {
        private readonly ConcurrentDictionary<string, TaskCompletionSource<IReadOnlyList<Character>>> _searches = new();
        public int SearchCount { get; private set; }
        public List<CharacterGroup> Groups { get; } = [];

        public Task<IReadOnlyList<Character>> SearchAsync(string? query, bool favoritesOnly)
        {
            SearchCount++;
            return _searches.GetOrAdd(
                query ?? string.Empty,
                _ => new TaskCompletionSource<IReadOnlyList<Character>>(
                    TaskCreationOptions.RunContinuationsAsynchronously)).Task;
        }

        public void Complete(string query, params Character[] characters) =>
            GetSearch(query).SetResult(characters);

        public void Fail(string query, Exception exception) =>
            GetSearch(query).SetException(exception);

        private TaskCompletionSource<IReadOnlyList<Character>> GetSearch(string query) =>
            _searches.GetOrAdd(
                query,
                _ => new TaskCompletionSource<IReadOnlyList<Character>>(
                    TaskCreationOptions.RunContinuationsAsynchronously));

        public Task<Character> SaveAsync(Character character, string? avatarSourcePath = null) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<CharacterGroup>> GetGroupsAsync() =>
            Task.FromResult<IReadOnlyList<CharacterGroup>>(Groups);

        public Task CreateGroupAsync(string name, IReadOnlyCollection<long> characterIds) =>
            throw new NotSupportedException();

        public Task UpdateGroupAsync(string originalName, string name, IReadOnlyCollection<long> characterIds) =>
            throw new NotSupportedException();

        public Task DeleteGroupAsync(string name) => throw new NotSupportedException();

        public Task<Character> ImportAsync(string path, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task DeleteAsync(Character character) => throw new NotSupportedException();
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public ConcurrentQueue<LogEntry> Entries { get; } = new();
        public IEnumerable<string> Messages => Entries.Select(x => x.Message);

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Enqueue(new LogEntry(logLevel, formatter(state, exception), exception));
    }

    private sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
}

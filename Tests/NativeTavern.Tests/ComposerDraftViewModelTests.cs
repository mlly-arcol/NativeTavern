using NativeTavern.Models;
using NativeTavern.ViewModels;
using Xunit;

namespace NativeTavern.Tests;

/// The composer is one shared box, so a draft only counts as saved when the view model routes it to
/// the conversation it was written for and brings it back after a restart.
public class ComposerDraftViewModelTests : IAsyncLifetime, IDisposable
{
    private readonly ChatViewModelHarness _harness = new();
    private ChatSession _first = null!;
    private ChatSession _second = null!;

    public async Task InitializeAsync()
    {
        var now = DateTimeOffset.UtcNow;
        _first = await _harness.CreateSessionAsync("甲对话", now.AddMinutes(-5));
        _second = await _harness.CreateSessionAsync("乙对话", now);
        await _harness.ViewModel.InitializeAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task TypingSavesTheDraftOfTheConversationBeingWrittenTo()
    {
        Assert.Equal(_second.Id, _harness.ViewModel.CurrentSession!.Id);
        _harness.ViewModel.InputText = "乙对话里没写完的一句";

        Assert.True(await WaitForDraftSavedAsync());
        var stored = Assert.Single(await _harness.Drafts.GetAllAsync());
        Assert.Equal(_second.Id, stored.ChatSessionId);
        Assert.Equal("乙对话里没写完的一句", stored.Text);
    }

    [Fact]
    public async Task SwitchingConversationsSwapsTheComposerContent()
    {
        _harness.ViewModel.InputText = "留在乙对话";
        Assert.True(await WaitForDraftSavedAsync());

        _harness.ViewModel.SelectedSession = _first;
        Assert.True(await ChatViewModelHarness.WaitForAsync(
            () => _harness.ViewModel.CurrentSession?.Id == _first.Id));
        Assert.Equal(string.Empty, _harness.ViewModel.InputText);
        Assert.Equal("留在乙对话", Assert.Single(await _harness.Drafts.GetAllAsync()).Text);

        _harness.ViewModel.SelectedSession = _second;
        Assert.True(await ChatViewModelHarness.WaitForAsync(
            () => _harness.ViewModel.CurrentSession?.Id == _second.Id));
        Assert.Equal("留在乙对话", _harness.ViewModel.InputText);
    }

    [Fact]
    public async Task ARestartBringsBackEveryDraftOnItsOwnConversation()
    {
        _harness.ViewModel.InputText = "重启也要在";
        Assert.True(await WaitForDraftSavedAsync());

        var restart = new ChatViewModelHarness(_harness.RootDirectory);
        try
        {
            await restart.ViewModel.InitializeAsync();
            Assert.Equal(_second.Id, restart.ViewModel.CurrentSession!.Id);
            Assert.Equal("重启也要在", restart.ViewModel.InputText);
        }
        finally { restart.Dispose(); }
    }

    private async Task<bool> WaitForDraftSavedAsync() => await ChatViewModelHarness.WaitForAsync(
        async () => (await _harness.Drafts.GetAllAsync()).Count > 0);
}

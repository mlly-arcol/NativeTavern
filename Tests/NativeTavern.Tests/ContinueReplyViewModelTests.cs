using NativeTavern.Models;
using NativeTavern.ViewModels;
using Xunit;

namespace NativeTavern.Tests;

/// 续写 has to grow the reply the character actually stopped on, and stay reversible when the model
/// takes the story somewhere the reader did not want it to go.
public class ContinueReplyViewModelTests : IAsyncLifetime, IDisposable
{
    private readonly ChatViewModelHarness _harness = new();

    /// Reply and 续写 requests are told apart by their last message, because suggestion and status
    /// requests interleave with them.
    public ContinueReplyViewModelTests()
    {
        _harness.Provider.Router = request => request.Messages.Last().Content.Contains("接着往下写")
            ? ["风从墙外", "翻进来。"]
            : ["山门", "已闭。"];
    }

    public async Task InitializeAsync()
    {
        await _harness.CreateSessionAsync("续写测试");
        await _harness.ViewModel.InitializeAsync();
        _harness.Provider.Release();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task ContinuationGrowsTheSameBubbleAndStaysReversible()
    {
        await SendAsync("关上门。");
        var bubble = ViewModel.Messages.Last();
        Assert.Equal("山门已闭。", bubble.Content);

        await ViewModel.ContinueLastReplyCommand.ExecuteAsync(bubble);
        Assert.True(await IdleAsync());

        Assert.Equal("山门已闭。风从墙外翻进来。", bubble.Content);
        Assert.True(bubble.SwipeCount >= 2);
        Assert.True(bubble.CanSwipeLeft);
        Assert.Single(ViewModel.Messages, x => x.IsAssistant);

        await ViewModel.SwipeLeftCommand.ExecuteAsync(bubble);
        Assert.Equal("山门已闭。", bubble.Content);
        await ViewModel.SwipeRightCommand.ExecuteAsync(bubble);
        Assert.Equal("山门已闭。风从墙外翻进来。", bubble.Content);
    }

    [Fact]
    public async Task TheAskingTurnAndTheWrittenPartAreBothSentToTheModel()
    {
        await SendAsync("关上门。");
        await ViewModel.ContinueLastReplyCommand.ExecuteAsync(ViewModel.Messages.Last());
        Assert.True(await IdleAsync());

        var request = _harness.Provider.SnapshotRequests()
            .First(x => x.Messages.Last().Content.Contains("接着往下写"));
        Assert.Equal("user", request.Messages[^1].Role);
        Assert.Equal("assistant", request.Messages[^2].Role);
        Assert.Contains("山门已闭。", request.Messages[^2].Content);
    }

    [Fact]
    public async Task AnEarlierReplyIsLeftAlone()
    {
        await SendAsync("第一问");
        var earlier = ViewModel.Messages.Last();
        await SendAsync("第二问");

        var requestsBefore = _harness.Provider.SnapshotRequests().Count;
        await ViewModel.ContinueLastReplyCommand.ExecuteAsync(earlier);

        Assert.Equal(requestsBefore, _harness.Provider.SnapshotRequests().Count);
        Assert.Equal("山门已闭。", earlier.Content);
        Assert.Contains("只能续写最后一条", ViewModel.ErrorMessage);
    }

    private ChatViewModel ViewModel => _harness.ViewModel;

    private async Task SendAsync(string text)
    {
        ViewModel.InputText = text;
        await ViewModel.SendCommand.ExecuteAsync(null);
        Assert.True(await IdleAsync());
    }

    private Task<bool> IdleAsync() => ChatViewModelHarness.WaitForAsync(
        () => !ViewModel.IsGenerating);
}

using NativeTavern.Models;
using NativeTavern.ViewModels;
using Xunit;

namespace NativeTavern.Tests;

/// A reply can take half a minute, and the next thing to say should not be held hostage by it.
public class QueuedSendViewModelTests : IAsyncLifetime, IDisposable
{
    private readonly ChatViewModelHarness _harness;
    private ChatSession _session = null!;

    public QueuedSendViewModelTests()
    {
        _harness = new ChatViewModelHarness();
    }

    public async Task InitializeAsync()
    {
        _session = await _harness.CreateSessionAsync("排队的对话");
        await _harness.ViewModel.InitializeAsync();
        Assert.Equal(_session.Id, _harness.ViewModel.CurrentSession!.Id);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task AMessageTypedDuringAReplyGoesOutWhenThatReplyFinishes()
    {
        _harness.ViewModel.InputText = "第一句";
        var inflight = _harness.ViewModel.SendCommand.ExecuteAsync(null);

        Assert.True(await ChatViewModelHarness.WaitForAsync(() => _harness.ViewModel.IsGenerating));
        _harness.ViewModel.InputText = "第二句，先排着";
        await _harness.ViewModel.SendCommand.ExecuteAsync(null);

        var queued = Assert.Single(_harness.ViewModel.QueuedSends);
        Assert.Equal("第二句，先排着", queued.Text);
        Assert.Equal(string.Empty, _harness.ViewModel.InputText);
        Assert.True(_harness.ViewModel.HasQueuedSends);

        _harness.Provider.Release();
        await inflight;
        Assert.True(await IdleWithMessagesAsync(4));

        Assert.Equal("第一句", _harness.ViewModel.Messages[0].Content);
        Assert.Equal("第二句，先排着", _harness.ViewModel.Messages[2].Content);
        Assert.Equal("夜色沉下来。", _harness.ViewModel.Messages[3].Content);
        Assert.Empty(_harness.ViewModel.QueuedSends);
        Assert.False(_harness.ViewModel.HasQueuedSends);
        Assert.False(_harness.ViewModel.IsGenerating);
    }

    [Fact]
    public async Task CancellingAQueuedMessageLetsTheRestThrough()
    {
        _harness.ViewModel.InputText = "开场";
        var inflight = _harness.ViewModel.SendCommand.ExecuteAsync(null);
        Assert.True(await ChatViewModelHarness.WaitForAsync(() => _harness.ViewModel.IsGenerating));

        _harness.ViewModel.InputText = "收回这句";
        await _harness.ViewModel.SendCommand.ExecuteAsync(null);
        _harness.ViewModel.InputText = "这句要发";
        await _harness.ViewModel.SendCommand.ExecuteAsync(null);
        Assert.Equal(2, _harness.ViewModel.QueuedSends.Count);

        _harness.ViewModel.CancelQueuedSendCommand.Execute(_harness.ViewModel.QueuedSends[0]);
        Assert.Equal("这句要发", Assert.Single(_harness.ViewModel.QueuedSends).Text);

        _harness.Provider.Release();
        await inflight;
        Assert.True(await IdleWithMessagesAsync(4));

        Assert.Equal("这句要发", _harness.ViewModel.Messages[2].Content);
        Assert.DoesNotContain("收回这句", _harness.ViewModel.Messages.Select(x => x.Content));
    }

    [Fact]
    public async Task ImageOnlyMessagesCanBeQueuedToo()
    {
        _harness.ViewModel.InputText = "看着这张图";
        var inflight = _harness.ViewModel.SendCommand.ExecuteAsync(null);
        Assert.True(await ChatViewModelHarness.WaitForAsync(() => _harness.ViewModel.IsGenerating));

        _harness.ViewModel.PendingImagePaths.Add("a.png");
        await _harness.ViewModel.SendCommand.ExecuteAsync(null);

        var queued = Assert.Single(_harness.ViewModel.QueuedSends);
        Assert.Equal(string.Empty, queued.Text);
        Assert.Equal("🖼1", queued.Preview);

        _harness.ViewModel.ClearQueuedSendsCommand.Execute(null);
        Assert.Empty(_harness.ViewModel.QueuedSends);

        _harness.Provider.Release();
        await inflight;
        Assert.True(await IdleAsync());
        Assert.Empty(ViewModel.QueuedSends);
    }

    /// A queued reply is finished only once the conversation is idle again, not merely when the fourth
    /// bubble appears.
    private Task<bool> IdleWithMessagesAsync(int minimum) => IdleAsync(
        () => ViewModel.Messages.Count >= minimum);

    private Task<bool> IdleAsync(Func<bool>? also = null) => ChatViewModelHarness.WaitForAsync(
        () => !ViewModel.IsGenerating && (also?.Invoke() ?? true));

    private ChatViewModel ViewModel => _harness.ViewModel;
}

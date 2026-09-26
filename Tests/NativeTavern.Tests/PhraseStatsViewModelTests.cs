using NativeTavern.Models;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

/// The pet-phrase list only helps if opening the statistics panel actually fills it in.
public class PhraseStatsViewModelTests : IAsyncLifetime, IDisposable
{
    private readonly ChatViewModelHarness _harness = new();

    public async Task InitializeAsync()
    {
        await _harness.CreateSessionAsync("口头禅面板");
        await _harness.ViewModel.InitializeAsync();
        _harness.Provider.Release();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task OpeningTheStatsPanelListsThePhraseTheModelKeepsUsing()
    {
        var phrase = await ReachATicAsync();

        Assert.Equal(3, phrase.Messages);
        Assert.Equal("嘴角微微上扬 ×3", phrase.ToString());
    }

    [Fact]
    public async Task TurningATicIntoARuleCleansLaterReplies()
    {
        var phrase = await ReachATicAsync();

        await _harness.ViewModel.CreateRewriteRuleCommand.ExecuteAsync(phrase);

        var script = Assert.Single(await _harness.Scripts.GetScriptsAsync());
        Assert.Equal("嘴角微微上扬", script.Pattern);
        Assert.Equal(string.Empty, script.Replacement);
        Assert.Equal(RegexScriptMode.Literal, script.Mode);
        Assert.Equal(RegexScriptTarget.AssistantOutput, script.Target);
        Assert.True(script.IsEnabled);

        _harness.Provider.Router = _ => ["他", "嘴角微微上扬，转身走了。"];
        _harness.ViewModel.InputText = "然后呢";
        await _harness.ViewModel.SendCommand.ExecuteAsync(null);
        Assert.True(await IdleAsync());

        var latest = _harness.ViewModel.Messages.Last(x => x.IsAssistant);
        Assert.DoesNotContain("嘴角微微上扬", latest.Content);
        Assert.Equal("他，转身走了。", latest.Content);
    }

    [Fact]
    public async Task PressingItTwiceDoesNotStackIdenticalRules()
    {
        var phrase = await ReachATicAsync();
        await _harness.ViewModel.CreateRewriteRuleCommand.ExecuteAsync(phrase);

        await _harness.ViewModel.CreateRewriteRuleCommand.ExecuteAsync(phrase);

        Assert.Single(await _harness.Scripts.GetScriptsAsync());
        Assert.Contains("已经有规则", _harness.ViewModel.ErrorMessage);
    }

    /// Three replies that share a tic, the statistics panel open, and the phrase in hand.
    private async Task<RepeatedPhrase> ReachATicAsync()
    {
        _harness.Provider.Router = _ => ["嘴角微微上扬。", "山门安静下来。"];
        for (var i = 0; i < 3; i++) await SendAsync($"第 {i} 次提问");

        _harness.ViewModel.OpenStatsCommand.Execute(null);
        Assert.True(_harness.ViewModel.IsStatsOpen);
        Assert.True(await ChatViewModelHarness.WaitForAsync(() => _harness.ViewModel.HasRepeatedPhrases));
        return _harness.ViewModel.RepeatedPhrases.Single(x => x.Phrase == "嘴角微微上扬");
    }

    private async Task SendAsync(string text)
    {
        _harness.ViewModel.InputText = text;
        await _harness.ViewModel.SendCommand.ExecuteAsync(null);
        Assert.True(await IdleAsync());
    }

    private Task<bool> IdleAsync() => ChatViewModelHarness.WaitForAsync(() => !_harness.ViewModel.IsGenerating);
}

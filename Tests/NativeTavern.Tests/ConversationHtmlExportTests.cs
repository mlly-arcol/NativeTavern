using System.Text;
using NativeTavern.Models;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

/// <summary>
/// The HTML copy is meant to be opened in a browser and printed, so model text must never be able to
/// write markup, and the tags that are generated have to stay balanced.
/// </summary>
public sealed class ConversationHtmlExportTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "nt-html-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public void ModelTextIsEscapedButActionEmphasisStillRenders()
    {
        var html = DataExportService.BuildConversationHtml(
            new ChatSession { Title = "界碑之外" },
            [new ChatMessage { Role = ChatRole.User, Content = "*皱眉* 你说<i>谎</i>" }]);

        Assert.Contains("<em>皱眉</em>", html, StringComparison.Ordinal);
        Assert.Contains("</em> 你说", html, StringComparison.Ordinal);
        Assert.Contains("&lt;i&gt;谎&lt;/i&gt;", html, StringComparison.Ordinal);
        Assert.DoesNotContain("<i>谎", html, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryTurnKeepsSpeakerTimestampAndPinnedMark()
    {
        var moment = new DateTimeOffset(2026, 3, 5, 22, 40, 0, TimeSpan.FromHours(8));
        var html = DataExportService.BuildConversationHtml(
            new ChatSession { Title = "界碑之外", ParentSessionId = 4 },
            [
                new ChatMessage { Role = ChatRole.User, Content = "此处埋的是什么？", IsPinned = true, CreatedAt = moment },
                new ChatMessage { Role = ChatRole.Assistant, Content = "一具会说话的骨。", CreatedAt = moment.AddMinutes(3) }
            ]);

        Assert.Contains("<h1>界碑之外</h1>", html, StringComparison.Ordinal);
        Assert.Contains("2 条消息", html, StringComparison.Ordinal);
        Assert.Contains("派生自更早的对话", html, StringComparison.Ordinal);
        Assert.Contains("class=\"user pinned\"", html, StringComparison.Ordinal);
        Assert.Contains("<span class=\"pin\"", html, StringComparison.Ordinal);
        Assert.Equal(2, CountOf(html, "<section"));
        Assert.Contains("2026-03-05", html, StringComparison.Ordinal);
        Assert.EndsWith("</html>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void TurnsWithoutARealDateSkipTheTimestamp()
    {
        var html = DataExportService.BuildConversationHtml(
            new ChatSession { Title = "无日期" },
            [new ChatMessage { Role = ChatRole.User, Content = "旧数据" }]);

        Assert.DoesNotContain("<time", html, StringComparison.Ordinal);
        Assert.Contains("<h2>User</h2>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void GroupSpeakersKeepTheirOwnNames()
    {
        var html = DataExportService.BuildConversationHtml(
            new ChatSession { Title = "群像", IsGroupChat = true },
            [new ChatMessage
            {
                Role = ChatRole.Assistant, SpeakerCharacterId = 7, Content = "我也来了。",
                CreatedAt = new DateTimeOffset(2026, 4, 1, 9, 30, 0, TimeSpan.FromHours(8))
            }],
            new Dictionary<long, string> { [7] = "云梦泽" });

        Assert.Contains("<h2>云梦泽<time", html, StringComparison.Ordinal);
    }

    [Fact]
    public void StrayAsterisksStayLiteralInsteadOfBreakingThePage()
    {
        var html = DataExportService.BuildConversationHtml(
            new ChatSession { Title = "算术" },
            [
                new ChatMessage { Role = ChatRole.User, Content = "一半 * 一半" },
                new ChatMessage { Role = ChatRole.Assistant, Content = "**双星号** 与 *单个*\n\n第二段" }
            ]);

        Assert.Contains("一半 * 一半", html, StringComparison.Ordinal);
        Assert.Contains("第二段", html, StringComparison.Ordinal);
        Assert.Equal(CountOf(html, "<em>"), CountOf(html, "</em>"));
    }

    [Fact]
    public async Task HtmlFilesAreWrittenAtomicallyAndOtherExtensionsStillFail()
    {
        Directory.CreateDirectory(_directory);
        var path = Path.Combine(_directory, "界碑之外.html");
        await DataExportService.ExportConversationAsync(
            new ChatSession { Title = "界碑之外" },
            [new ChatMessage { Role = ChatRole.User, Content = "第一句" }],
            null, path);

        var written = await File.ReadAllTextAsync(path, Encoding.UTF8);
        Assert.StartsWith("<!DOCTYPE html>", written, StringComparison.Ordinal);
        Assert.Contains("<title>界碑之外</title>", written, StringComparison.Ordinal);
        Assert.Empty(Directory.GetFiles(_directory, ".*.tmp"));

        await Assert.ThrowsAsync<InvalidDataException>(() => DataExportService.ExportConversationAsync(
            new ChatSession { Title = "x" }, [], null, Path.Combine(_directory, "notes.txt")));
    }

    private static int CountOf(string haystack, string needle)
    {
        var count = 0;
        for (var index = haystack.IndexOf(needle, StringComparison.Ordinal);
             index >= 0;
             index = haystack.IndexOf(needle, index + needle.Length, StringComparison.Ordinal))
        {
            count++;
        }
        return count;
    }

    public void Dispose()
    {
        try { Directory.Delete(_directory, true); } catch (IOException) { }
    }
}

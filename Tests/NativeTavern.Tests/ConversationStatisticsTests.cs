using NativeTavern.Models;
using NativeTavern.Services;
using NativeTavern.ViewModels;
using Xunit;

namespace NativeTavern.Tests;

public class ConversationStatisticsTests
{
    private static ChatMessageViewModel Message(ChatRole role, string content, string? speaker = null, bool streaming = false)
    {
        var view = new ChatMessageViewModel(
            new ChatMessage { Role = role, Content = content, CreatedAt = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc) },
            assistantName: speaker);
        if (streaming) view.IsStreaming = true;
        return view;
    }

    private static string Value(IReadOnlyList<StatLine> lines, string label) =>
        Assert.Single(lines, line => line.Label == label).Value;

    [Fact]
    public void WhitespaceIsNotCountedAsText()
    {
        var lines = ConversationStatistics.BuildLines(
            [Message(ChatRole.User, "你好 世界"), Message(ChatRole.Assistant, "碑 在 此", "守墓人")], null, null);
        Assert.Equal("7（我 4 · 角色 3）", Value(lines, "字数"));
        Assert.Equal("2 条（我 1 · 角色 1）", Value(lines, "消息"));
        Assert.Equal("3 字", Value(lines, "平均角色回复"));
    }

    [Fact]
    public void StreamingPlaceholdersAreIgnored()
    {
        var lines = ConversationStatistics.BuildLines(
            [Message(ChatRole.User, "一二三"), Message(ChatRole.Assistant, "", "守墓人", streaming: true)], null, null);
        Assert.StartsWith("1 条", Value(lines, "消息"));
        Assert.Equal("—", Value(lines, "平均角色回复"));
    }

    [Fact]
    public void SpeakerSharesAddUpToExactlyOneHundred()
    {
        var shares = ConversationStatistics.BuildSpeakerShares(
            [
                Message(ChatRole.Assistant, "一", "甲"),
                Message(ChatRole.Assistant, "二", "乙"),
                Message(ChatRole.Assistant, "三", "丙")
            ]);
        Assert.Equal(3, shares.Count);
        Assert.Equal(100, shares.Sum(x => x.Percent));
        Assert.All(shares, share => Assert.InRange(share.Percent, 33, 34));
    }

    [Fact]
    public void SpeakerSharesOrderByVolumeAndSkipEmptySpeakers()
    {
        var shares = ConversationStatistics.BuildSpeakerShares(
            [
                Message(ChatRole.Assistant, "短短短", "甲"),
                Message(ChatRole.Assistant, "长长长长", "乙"),
                Message(ChatRole.Assistant, "   ", "丙"),
                Message(ChatRole.User, "我的消息")
            ]);
        Assert.Equal(new[] { "乙", "甲" }, shares.Select(x => x.Name));
        Assert.Equal(new[] { 57, 43 }, shares.Select(x => x.Percent));
    }

    [Fact]
    public void EmptyConversationHasNoShares()
    {
        Assert.Empty(ConversationStatistics.BuildSpeakerShares([]));
    }

    [Theory]
    [InlineData(0, "不足 1 分钟")]
    [InlineData(5, "5 分钟")]
    [InlineData(125, "2 小时 5 分钟")]
    [InlineData(1500, "1 天 1 小时")]
    public void SpanIsDescribedInHumanUnits(int minutes, string expected)
    {
        var first = new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc);
        var lines = ConversationStatistics.BuildLines(
            [Message(ChatRole.User, "一")], first, first.AddMinutes(minutes));
        Assert.Equal(expected, Value(lines, "时间跨度"));
    }

    [Fact]
    public void MissingTimestampsRenderAsPlaceholder()
    {
        Assert.Equal("—", Value(ConversationStatistics.BuildLines([Message(ChatRole.User, "一")], null, null), "时间跨度"));
    }

    [Fact]
    public void PinsAndSwipesAreCounted()
    {
        var pinned = Message(ChatRole.Assistant, "内容", "守墓人");
        pinned.IsPinned = true;
        pinned.SetSwipeState(0, 3, "内容");
        var lines = ConversationStatistics.BuildLines([pinned, Message(ChatRole.User, "我的")], null, null);
        Assert.Equal("1 条 / 0 张", Value(lines, "收藏 / 图片"));
        Assert.Equal("3 条", Value(lines, "候选回复"));
    }
}

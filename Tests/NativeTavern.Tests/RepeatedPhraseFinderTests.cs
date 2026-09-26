using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

/// Models fall into pet phrases, and a writer needs to see them without counting by hand. The counter is
/// deliberately reply-based: one purple paragraph should not outrank a tic spread across a chapter.
public class RepeatedPhraseFinderTests
{
    /// Every reply shares only its opening phrase - the rest is unique filler, so a test can be sure
    /// that what the finder reports really is the phrase it planted.
    private static string[] Say(params string[] openers) =>
        openers.Select((opener, index) => opener + Body(index)).ToArray();

    private static string Body(int index) =>
        new(Enumerable.Range(0, 22).Select(i => (char)('一' + ((index * 31 + i * 17) % 300))).ToArray());

    private static bool Reports(IReadOnlyList<RepeatedPhrase> found, string phrase, int replies) =>
        found.Any(x => x.Phrase == phrase && x.Messages == replies);

    [Fact]
    public void FindsAPhraseThatKeepsComingBack()
    {
        var found = RepeatedPhraseFinder.Find(Say("嘴角微微上扬", "嘴角微微上扬", "嘴角微微上扬", "他顿了顿脚步"));

        Assert.True(Reports(found, "嘴角微微上扬", 3));
    }

    [Fact]
    public void RepeatsInsideOneReplyCountOnce()
    {
        var found = RepeatedPhraseFinder.Find(Say(
            "嘴角微微上扬。嘴角微微上扬。嘴角微微上扬。", "嘴角微微上扬", "嘴角微微上扬"));

        Assert.True(Reports(found, "嘴角微微上扬", 3));
    }

    [Fact]
    public void TheLongestFormOfATicIsTheOneThatShowsUp()
    {
        var phrases = RepeatedPhraseFinder.Find(Say("嘴角微微上扬", "嘴角微微上扬", "嘴角微微上扬"))
            .Select(x => x.Phrase).ToList();

        Assert.Contains("嘴角微微上扬", phrases);
        Assert.DoesNotContain("嘴角微微", phrases);
        Assert.DoesNotContain("角微微上", phrases);
    }

    [Fact]
    public void PhrasesUsedOnceOrTwiceAreNotTics()
    {
        Assert.DoesNotContain("眉目清秀",
            RepeatedPhraseFinder.Find(Say("眉目清秀", "眉目清秀", "他转身离开")).Select(x => x.Phrase));
        Assert.Contains("眉目清秀",
            RepeatedPhraseFinder.Find(Say("眉目清秀", "眉目清秀", "眉目清秀")).Select(x => x.Phrase));
    }

    [Fact]
    public void PunctuationInTheMiddleDoesNotHideATic()
    {
        var found = RepeatedPhraseFinder.Find(new[]
        {
            "嘴角，微微上扬。" + Body(1), "嘴角微微 上扬" + Body(2), "他抬起头，嘴角微微上扬" + Body(3)
        });

        Assert.True(Reports(found, "嘴角微微上扬", 3));
    }

    [Fact]
    public void OnlyTheNewestRepliesAreScanned()
    {
        var replies = new[] { "远古的口头禅" + Body(8), "远古的口头禅" + Body(9), "远古的口头禅" + Body(10) }
            .Concat(Enumerable.Range(20, 120).Select(index => "更新一些的描写" + Body(index)))
            .ToArray();

        Assert.DoesNotContain("远古的口头禅", RepeatedPhraseFinder.Find(replies).Select(x => x.Phrase));
    }

    [Fact]
    public void TheListOfTicsStaysShortEnoughToRead()
    {
        var replies = new List<string>();
        for (var index = 0; index < 15; index++)
            for (var copy = 0; copy < 3; copy++)
                replies.Add($"第{index}个说法" + Body(index * 3 + copy));

        Assert.InRange(RepeatedPhraseFinder.Find(replies).Count, 1, 12);
    }

    [Fact]
    public void NothingToReportForAnEmptyOrTinyConversation()
    {
        Assert.Empty(RepeatedPhraseFinder.Find([]));
        Assert.Empty(RepeatedPhraseFinder.Find(["", "   ", "只有一句：嘴角微微上扬，嘴角微微上扬"]));
    }

    [Fact]
    public void AReplyChipReadsAsPhraseTimesReplies()
    {
        Assert.Equal("嘴角微微上扬 ×3", new RepeatedPhrase("嘴角微微上扬", 3).ToString());
    }
}

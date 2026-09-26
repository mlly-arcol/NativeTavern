using NativeTavern.ViewModels;
using Xunit;

namespace NativeTavern.Tests;

/// Messages typed while a reply is still streaming wait their turn, and only for the conversation
/// they were typed in.
public class SendQueueTests
{
    private static SendQueue.Item Item(string text, params string[] images) =>
        new(text, images);

    [Fact]
    public void ItemsGoOutInTheOrderTheyWereQueued()
    {
        var queue = new SendQueue();
        queue.Enqueue(1, Item("第一条"));
        queue.Enqueue(1, Item("第二条"));

        Assert.Equal("第一条", queue.Dequeue(1)!.Text);
        Assert.Equal("第二条", queue.Dequeue(1)!.Text);
        Assert.Null(queue.Dequeue(1));
    }

    [Fact]
    public void EachConversationKeepsItsOwnLine()
    {
        var queue = new SendQueue();
        queue.Enqueue(1, Item("甲的追问"));
        queue.Enqueue(2, Item("乙的追问"));

        Assert.Equal("乙的追问", queue.Dequeue(2)!.Text);
        Assert.Null(queue.Dequeue(2));
        Assert.Equal(["甲的追问"], queue.For(1).Select(x => x.Text));
    }

    [Fact]
    public void CancellingRemovesOnlyTheClickedItem()
    {
        var queue = new SendQueue();
        var middle = Item("取消我");
        queue.Enqueue(1, Item("第一"));
        queue.Enqueue(1, middle);
        queue.Enqueue(1, Item("第三"));

        Assert.True(queue.Remove(1, middle));
        Assert.False(queue.Remove(1, middle));
        Assert.Equal(["第一", "第三"], queue.For(1).Select(x => x.Text));
    }

    [Fact]
    public void IdenticalMessagesAreCancelledOneAtATime()
    {
        var queue = new SendQueue();
        queue.Enqueue(1, Item("再来一次"));
        queue.Enqueue(1, Item("再来一次"));

        queue.Remove(1, Item("再来一次"));
        Assert.Single(queue.For(1));
    }

    [Fact]
    public void ClearingOneConversationLeavesTheOthersAlone()
    {
        var queue = new SendQueue();
        queue.Enqueue(1, Item("甲"));
        queue.Enqueue(2, Item("乙"));

        queue.Clear(1);
        Assert.Empty(queue.For(1));
        Assert.Single(queue.For(2));
    }

    [Fact]
    public void ChipsShowAShortReadablePreview()
    {
        Assert.Equal("带图 🖼1", Item("带图", "a.png").Preview);
        Assert.Equal("🖼2", Item(string.Empty, "a.png", "b.png").Preview);
        Assert.Equal("两行 压平", Item("两行\n压平").Preview);
        Assert.Equal(new string('长', 40) + "…", Item(new string('长', 45)).Preview);
    }
}

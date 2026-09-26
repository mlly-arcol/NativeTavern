using NativeTavern.ViewModels;
using Xunit;

namespace NativeTavern.Tests;

/// The composer is shared by every conversation, so drafts have to travel with the conversation that
/// owns them - and a slow-loading conversation must never overwrite text typed while it was loading.
public class DraftStoreTests
{
    private const long A = 1;
    private const long B = 2;

    private static DraftStore.Draft Capture(string text, params string[] images) =>
        DraftStore.Capture(text, images);

    [Fact]
    public void SwitchingAwayAndBackRestoresTheDraft()
    {
        var store = new DraftStore();
        var atStart = Capture("unfinished note for A");

        store.OnLoadStarted(A, atStart);
        var onB = store.OnLoadFinished(B, atStart, Capture(string.Empty));
        Assert.Equal(string.Empty, onB.Text);

        store.OnLoadStarted(B, onB);
        var backOnA = store.OnLoadFinished(A, Capture(string.Empty), Capture(string.Empty));
        Assert.Equal("unfinished note for A", backOnA.Text);
    }

    [Fact]
    public void ImagesTravelWithTheirConversation()
    {
        var store = new DraftStore();
        var atStart = Capture("with shots", "a.png", "b.png");

        store.OnLoadStarted(A, atStart);
        store.OnLoadFinished(B, atStart, Capture(string.Empty));
        store.OnLoadStarted(B, Capture(string.Empty));

        var backOnA = store.OnLoadFinished(A, Capture(string.Empty), Capture(string.Empty));
        Assert.Equal(new[] { "a.png", "b.png" }, backOnA.Images);
    }

    [Fact]
    public void TextTypedWhileTheConversationLoadsOutranksTheStoredDraft()
    {
        var store = new DraftStore();
        store.OnLoadStarted(A, Capture("old draft"));
        store.OnLoadStarted(B, Capture(string.Empty));

        var loaded = store.OnLoadFinished(B, Capture(string.Empty), Capture("typed mid-flight"));
        Assert.Equal("typed mid-flight", loaded.Text);
        Assert.Equal("typed mid-flight", store.Peek(B)!.Text);
    }

    [Fact]
    public void ReloadingTheSameConversationKeepsTheInProgressText()
    {
        var store = new DraftStore();
        var composer = Capture("half written");
        store.OnLoadStarted(A, composer);

        var result = store.OnLoadFinished(A, composer, composer);
        Assert.Equal("half written", result.Text);
    }

    [Fact]
    public void SendingClearsTheMemoryForThatConversationOnly()
    {
        var store = new DraftStore();
        store.OnLoadStarted(A, Capture("note for A"));
        store.OnLoadStarted(B, Capture("note for B"));
        store.Forget(A);

        Assert.Null(store.Peek(A));
        Assert.Equal("note for B", store.Peek(B)!.Text);
        Assert.Equal(string.Empty, store.OnLoadFinished(A, Capture(string.Empty), Capture(string.Empty)).Text);
    }

    [Fact]
    public void EmptyDraftsAreNotRemembered()
    {
        var store = new DraftStore();
        store.Remember(A, Capture("text", "a.png"));
        store.Remember(A, Capture(string.Empty));
        Assert.Null(store.Peek(A));
        Assert.True(Capture(string.Empty).IsEmpty);
        Assert.False(Capture(string.Empty, "a.png").IsEmpty);
    }

    [Fact]
    public void ImageOrderIsPartOfTheComposerState()
    {
        Assert.False(Capture("", "a.png", "b.png").SameAs(Capture("", "b.png", "a.png")));
        Assert.True(Capture("x", "a.png").SameAs(Capture("x", "a.png")));
    }
}

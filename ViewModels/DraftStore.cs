namespace NativeTavern.ViewModels;

/// Remembers what the composer held for each conversation, so switching away and back restores an
/// unsent message instead of throwing it away. Conversation loading is asynchronous and a long chat
/// can take seconds, so the store also decides whose text wins when both a stored draft and freshly
/// typed text exist for the same conversation.
public sealed class DraftStore
{
    public sealed record Draft(string Text, IReadOnlyList<string> Images)
    {
        public static readonly Draft Empty = new(string.Empty, []);

        public bool IsEmpty => string.IsNullOrEmpty(Text) && Images.Count == 0;

        // A record compares its list member by reference, so compare the images themselves.
        public bool SameAs(Draft other) => Text == other.Text && Images.SequenceEqual(other.Images);
    }

    private readonly Dictionary<long, Draft> _drafts = [];

    public static Draft Capture(string text, IEnumerable<string> images) => new(text, images.ToArray());

    public Draft? Peek(long sessionId) => _drafts.GetValueOrDefault(sessionId);

    public void Forget(long sessionId) => _drafts.Remove(sessionId);

    public void Remember(long sessionId, Draft draft)
    {
        if (draft.IsEmpty) _drafts.Remove(sessionId);
        else _drafts[sessionId] = draft;
    }

    /// Files away whatever the composer held the moment a conversation starts loading.
    public void OnLoadStarted(long? currentSessionId, Draft composer)
    {
        if (currentSessionId is long id) Remember(id, composer);
    }

    /// Decides what the composer should show once the conversation has loaded. Anything typed while
    /// the load was in flight belongs to the conversation that just appeared, so it outranks the
    /// stored draft instead of being overwritten by it.
    public Draft OnLoadFinished(long sessionId, Draft composerAtLoadStart, Draft composerNow)
    {
        var result = composerNow.SameAs(composerAtLoadStart) ? Peek(sessionId) ?? Draft.Empty : composerNow;
        Remember(sessionId, result);
        return result;
    }
}

namespace NativeTavern.ViewModels;

/// Messages the user sent while a reply was still being written. They belong to the conversation they
/// were typed into, so switching away keeps a queue waiting instead of firing it into the wrong chat.
public sealed class SendQueue
{
    public sealed record Item(string Text, IReadOnlyList<string> Images)
    {
        public string Preview
        {
            get
            {
                var text = Text.Replace('\r', ' ').Replace('\n', ' ').Trim();
                if (text.Length > 40) text = text[..40] + "…";
                var pictures = Images.Count > 0 ? $" 🖼{Images.Count}" : string.Empty;
                if (text.Length == 0) return pictures.Length > 0 ? pictures.Trim() : string.Empty;
                return text + pictures;
            }
        }

        // Without this a chip or popup entry is announced as the view-model type name.
        public override string ToString() => Preview;
    }

    private readonly Dictionary<long, List<Item>> _bySession = [];

    public IReadOnlyList<Item> For(long sessionId) =>
        _bySession.TryGetValue(sessionId, out var items) ? items.ToArray() : [];

    public void Enqueue(long sessionId, Item item)
    {
        if (!_bySession.TryGetValue(sessionId, out var items)) _bySession[sessionId] = items = [];
        items.Add(item);
    }

    public bool Remove(long sessionId, Item item)
    {
        if (!_bySession.TryGetValue(sessionId, out var items)) return false;
        var removed = items.Remove(item);
        if (items.Count == 0) _bySession.Remove(sessionId);
        return removed;
    }

    /// Takes the oldest queued message, or null when that conversation has nothing waiting.
    public Item? Dequeue(long sessionId)
    {
        if (!_bySession.TryGetValue(sessionId, out var items) || items.Count == 0) return null;
        var item = items[0];
        Remove(sessionId, item);
        return item;
    }

    public void Clear(long sessionId) => _bySession.Remove(sessionId);
}

namespace NativeTavern.Models;

/// Everything needed to paint a conversation, gathered in a handful of queries instead of one round
/// trip per message.
public sealed record SessionBundle(
    IReadOnlyList<ChatMessage> Messages,
    IReadOnlyDictionary<long, int> SwipeCounts,
    IReadOnlyDictionary<long, IReadOnlyList<ChatAttachment>> AttachmentsByMessage)
{
    public int SwipeCountFor(long messageId) => SwipeCounts.GetValueOrDefault(messageId);

    public IReadOnlyList<ChatAttachment> AttachmentsFor(long messageId) =>
        AttachmentsByMessage.TryGetValue(messageId, out var attachments) ? attachments : [];
}

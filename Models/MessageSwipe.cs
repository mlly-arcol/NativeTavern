namespace NativeTavern.Models;

public sealed class MessageSwipe
{
    public long Id { get; set; }
    public long ChatMessageId { get; set; }
    public int SwipeIndex { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
}

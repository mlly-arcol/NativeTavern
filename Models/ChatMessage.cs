namespace NativeTavern.Models;

public sealed class ChatMessage
{
    public long Id { get; set; }
    public long ChatSessionId { get; set; }
    public ChatRole Role { get; set; }
    public long? SpeakerCharacterId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? UpdatedAt { get; set; }
    public int CurrentSwipeIndex { get; set; }
}

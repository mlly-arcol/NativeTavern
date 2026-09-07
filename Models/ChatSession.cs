namespace NativeTavern.Models;

public sealed class ChatSession
{
    public long Id { get; set; }
    public string Title { get; set; } = "New Chat";
    public long? CharacterId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

namespace NativeTavern.Models;

public sealed class ChatSession
{
    public long Id { get; set; }
    public string Title { get; set; } = "New Chat";
    public long? CharacterId { get; set; }
    public bool IsGroupChat { get; set; }
    public long? PersonaId { get; set; }
    public long? LorebookId { get; set; }
    public long? PromptPresetId { get; set; }
    public string AuthorNote { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

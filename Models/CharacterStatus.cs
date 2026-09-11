namespace NativeTavern.Models;

public sealed class CharacterStatusSnapshot
{
    public long ChatSessionId { get; set; }
    public long CharacterId { get; set; }
    public string CharacterName { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyList<CharacterStatusAttribute> Attributes { get; set; } = [];
    public long? SourceMessageId { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class CharacterStatusAttribute
{
    public string Name { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

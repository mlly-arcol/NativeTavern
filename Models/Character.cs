namespace NativeTavern.Models;

public sealed class Character
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Personality { get; set; } = string.Empty;
    public string Scenario { get; set; } = string.Empty;
    public string FirstMessage { get; set; } = string.Empty;
    public string ExampleMessages { get; set; } = string.Empty;
    public string Creator { get; set; } = string.Empty;
    public string Tags { get; set; } = string.Empty;
    public string GroupName { get; set; } = string.Empty;
    /// <summary>Comma separated nicknames; they let search and the speaker filter find this character.</summary>
    public string Aliases { get; set; } = string.Empty;
    public IReadOnlyList<string> AliasList => Aliases
        .Split([',', '，'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(alias => alias.Length > 0)
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();
    public bool IsFavorite { get; set; }
    public string AvatarPath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

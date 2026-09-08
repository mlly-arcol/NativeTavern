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
    public bool IsFavorite { get; set; }
    public string AvatarPath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

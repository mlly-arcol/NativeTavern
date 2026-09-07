namespace NativeTavern.Models;

public sealed class Persona
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Lorebook
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class LoreEntry
{
    public long Id { get; set; }
    public long LorebookId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Keywords { get; set; } = string.Empty;
    public string SecondaryKeywords { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Priority { get; set; } = 100;
    public int Depth { get; set; } = 4;
    public bool IsEnabled { get; set; } = true;
    public bool IsConstant { get; set; }
    public bool IsSelective { get; set; }
}

public sealed class PromptPreset
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SystemPrompt { get; set; } = string.Empty;
    public string MainPrompt { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double? Temperature { get; set; }
    public double? TopP { get; set; }
    public int? MaxTokens { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class PromptBuildResult
{
    public IReadOnlyList<ChatCompletionMessage> Messages { get; init; } = [];
    public string Model { get; init; } = string.Empty;
    public double Temperature { get; init; }
    public double TopP { get; init; }
    public int MaxTokens { get; init; }
    public IReadOnlyList<string> ActivatedLoreEntries { get; init; } = [];
}

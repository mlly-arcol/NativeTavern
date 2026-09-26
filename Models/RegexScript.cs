namespace NativeTavern.Models;

public enum RegexScriptTarget
{
    /// <summary>Applies to what the user types before it is stored and sent.</summary>
    UserInput,

    /// <summary>Applies to what the model returns before it is stored and shown.</summary>
    AssistantOutput,
}

public enum RegexScriptMode
{
    /// <summary>Plain find and replace, no pattern syntax.</summary>
    Literal,

    /// <summary>.NET regular expression.</summary>
    Regex,
}

public sealed class RegexScript
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Pattern { get; set; } = string.Empty;
    public string Replacement { get; set; } = string.Empty;
    public RegexScriptTarget Target { get; set; } = RegexScriptTarget.AssistantOutput;
    public RegexScriptMode Mode { get; set; } = RegexScriptMode.Regex;
    public bool IsEnabled { get; set; } = true;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

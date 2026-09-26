namespace NativeTavern.Models;

/// <summary>One message matched by the conversation search, carrying enough context to jump to it.</summary>
public sealed class MessageSearchResult
{
    public long MessageId { get; init; }
    public long ChatSessionId { get; init; }
    public string SessionTitle { get; init; } = string.Empty;
    public ChatRole Role { get; init; }
    public DateTimeOffset CreatedAt { get; init; }

    /// <summary>Character that spoke, when the hit belongs to one; empty for user turns.</summary>
    public string Speaker { get; init; } = string.Empty;

    /// <summary>Matched text with surrounding context, trimmed to one or two lines.</summary>
    public string Snippet { get; init; } = string.Empty;

    /// <summary>Screen-reader label; without it the hit is announced as this type's name.</summary>
    public string Label => $"{SessionTitle} · {(string.IsNullOrWhiteSpace(Speaker) ? string.Empty : Speaker + "：")}{Snippet}";

    public override string ToString() => Label;
}

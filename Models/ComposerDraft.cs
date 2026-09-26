namespace NativeTavern.Models;

/// Composer content that was typed into a conversation but never sent.
public sealed record ComposerDraft(long ChatSessionId, string Text, IReadOnlyList<string> Images);

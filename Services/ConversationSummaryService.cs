using NativeTavern.Data.Repositories;
using NativeTavern.Models;

namespace NativeTavern.Services;

public sealed class ConversationSummaryService(ChatMessageRepository messages, ChatSessionRepository sessions)
{
    public async Task UpdateIfNeededAsync(ChatSession session)
    {
        var history = await messages.GetBySessionAsync(session.Id);
        if (history.Count < 20) return;
        var earlier = history.Take(Math.Max(0, history.Count - 10)).ToList();
        var lines = earlier.Select(message => $"{message.Role}: {Trim(message.Content, 260)}");
        var summary = "Automatic conversation summary (earlier context):\n" + string.Join("\n", lines);
        if (summary.Length > 6000) summary = summary[..6000];
        if (summary == session.Summary) return;
        session.Summary = summary;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await sessions.UpdateAsync(session);
    }

    private static string Trim(string content, int max) => content.Length <= max ? content : content[..max] + "…";
}

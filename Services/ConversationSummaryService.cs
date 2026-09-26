using NativeTavern.Data.Repositories;
using NativeTavern.Models;

namespace NativeTavern.Services;

/// <summary>
/// Keeps a rolling recap of the earlier turns so a long roleplay still fits the context window.
/// Only the turns nobody has summarized yet are read on each update, and a recap the writer edited by
/// hand is never overwritten automatically.
/// </summary>
public sealed class ConversationSummaryService(ChatMessageRepository messages, ChatSessionRepository sessions)
{
    public const int MinimumMessages = 20;
    public const int RecentMessages = 10;
    public const string Header = "Automatic conversation summary (earlier context):";
    public const int MaxLines = 24;
    public const int MaxCharacters = 6000;
    public const int ManualLimit = 8000;
    private const int LineContentLimit = 260;

    public async Task UpdateIfNeededAsync(ChatSession session)
    {
        if (session.SummaryIsManual) return;
        var history = await messages.GetBySessionAsync(session.Id);
        if (history.Count < MinimumMessages) return;
        var coverable = history.Count - RecentMessages;
        if (coverable < session.SummaryCoveredCount)
        {
            await RebuildAsync(session);
            return;
        }
        if (coverable == session.SummaryCoveredCount) return;

        var fresh = history.Skip(session.SummaryCoveredCount).Take(coverable - session.SummaryCoveredCount);
        var lines = ReadLines(session).Concat(fresh.Select(ToLine)).ToList();
        await SaveAsync(session, Compact(lines), coverable);
    }

    /// <summary>Recomputes the recap from scratch, for example after a back-edited message.</summary>
    public async Task RebuildAsync(ChatSession session)
    {
        var history = await messages.GetBySessionAsync(session.Id);
        var coverable = Math.Max(0, history.Count - RecentMessages);
        var lines = history.Take(coverable).Select(ToLine).ToList();
        await SaveAsync(session, Compact(lines), coverable);
    }

    /// <summary>Stores the writer's own recap and pauses the automatic digest until the next rebuild.</summary>
    public async Task SaveManualAsync(ChatSession session, string summary)
    {
        summary = summary.Trim();
        if (summary.Length > ManualLimit)
            throw new InvalidOperationException($"剧情回顾不能超过 {ManualLimit} 个字符。");
        session.Summary = summary;
        session.SummaryIsManual = summary.Length > 0;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await sessions.UpdateAsync(session);
    }

    public async Task ClearAsync(ChatSession session)
    {
        session.Summary = string.Empty;
        session.SummaryCoveredCount = 0;
        session.SummaryIsManual = false;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await sessions.UpdateAsync(session);
    }

    private async Task SaveAsync(ChatSession session, IReadOnlyList<string> lines, int coveredCount)
    {
        session.Summary = lines.Count == 0 ? string.Empty : Header + "\n" + string.Join("\n", lines);
        session.SummaryCoveredCount = coveredCount;
        // Any automatic write hands control back to the digest, including right after a manual recap.
        session.SummaryIsManual = false;
        session.UpdatedAt = DateTimeOffset.UtcNow;
        await sessions.UpdateAsync(session);
    }

    private static IReadOnlyList<string> ReadLines(ChatSession session) =>
        session.Summary.StartsWith(Header, StringComparison.Ordinal)
            ? session.Summary.Split('\n').Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).ToList()
            : [];

    private static string ToLine(ChatMessage message) =>
        $"{message.Role}: {Trim(message.Content, LineContentLimit)}";

    /// <summary>Oldest lines are dropped first: the newest covered turns matter most.</summary>
    private static List<string> Compact(IReadOnlyList<string> lines)
    {
        var kept = lines.Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
        while (kept.Count > MaxLines) kept.RemoveAt(0);
        var characters = kept.Sum(line => line.Length + 1);
        while (kept.Count > 1 && characters > MaxCharacters)
        {
            characters -= kept[0].Length + 1;
            kept.RemoveAt(0);
        }
        if (kept.Count == 1 && kept[0].Length > MaxCharacters) kept[0] = kept[0][..MaxCharacters];
        return kept;
    }

    private static string Trim(string content, int max) => content.Length <= max ? content : content[..max] + "…";
}

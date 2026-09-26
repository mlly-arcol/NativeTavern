using NativeTavern.ViewModels;

namespace NativeTavern.Services;

/// <summary>One speaker's share of the written text in a conversation.</summary>
public sealed class SpeakerShare(string name, int characters, int percent)
{
    public string Name { get; } = name;
    public int Characters { get; } = characters;
    public int Percent { get; } = percent;
    public override string ToString() => $"{Name} {Percent}%";
}

/// <summary>A label/value pair shown in the statistics panel.</summary>
public sealed class StatLine(string label, string value)
{
    public string Label { get; } = label;
    public string Value { get; } = value;
    public override string ToString() => $"{Label} {Value}";
}

/// <summary>
/// Read-only roll-up of the conversation currently in memory. Character counts ignore whitespace because
/// Chinese prose is measured in 字, and speaker percentages are rounded with the largest-remainder method
/// so the displayed shares always add up to 100.
/// </summary>
public static class ConversationStatistics
{
    public static IReadOnlyList<StatLine> BuildLines(
        IEnumerable<ChatMessageViewModel> messages, DateTimeOffset? firstAt, DateTimeOffset? lastAt)
    {
        var items = messages.Where(x => !x.IsStreaming).ToList();
        var assistant = items.Where(x => x.IsAssistant).ToList();
        var user = items.Where(x => !x.IsAssistant).ToList();
        var assistantCharacters = CountCharacters(assistant.Select(x => x.Content));
        var userCharacters = CountCharacters(user.Select(x => x.Content));
        var lines = new List<StatLine>
        {
            new("消息", $"{Format(items.Count)} 条（我 {Format(user.Count)} · 角色 {Format(assistant.Count)}）"),
            new("字数", Format(assistantCharacters + userCharacters) +
                $"（我 {Format(userCharacters)} · 角色 {Format(assistantCharacters)}）"),
            new("平均角色回复", assistant.Count == 0 ? "—" : Format(assistantCharacters / assistant.Count) + " 字"),
            new("候选回复", Format(assistant.Sum(x => Math.Max(0, x.SwipeCount))) + " 条"),
            new("收藏 / 图片", $"{items.Count(x => x.IsPinned)} 条 / {items.Sum(x => x.Attachments.Count)} 张"),
            new("时间跨度", DescribeSpan(firstAt, lastAt))
        };
        return lines;
    }

    public static IReadOnlyList<SpeakerShare> BuildSpeakerShares(IEnumerable<ChatMessageViewModel> messages)
    {
        var bySpeaker = messages
            .Where(x => x.IsAssistant && !x.IsStreaming)
            .GroupBy(x => string.IsNullOrWhiteSpace(x.RoleLabel) ? "角色" : x.RoleLabel)
            .Select(group => (Name: group.Key, Characters: CountCharacters(group.Select(x => x.Content))))
            .Where(x => x.Characters > 0)
            .OrderByDescending(x => x.Characters)
            .ToList();
        var total = bySpeaker.Sum(x => x.Characters);
        if (total == 0) return [];
        // Largest remainder: floor every share, then hand out the leftover points to the biggest fractions.
        var exact = bySpeaker.Select(x => (double)x.Characters * 100 / total).ToList();
        var floors = exact.Select(Math.Floor).ToList();
        var percent = bySpeaker.Select((_, index) => (int)floors[index]).ToList();
        var leftover = 100 - percent.Sum();
        var order = Enumerable.Range(0, exact.Count)
            .OrderByDescending(index => exact[index] - floors[index])
            .Take(leftover);
        foreach (var index in order) percent[index]++;
        return bySpeaker.Select((speaker, index) => new SpeakerShare(speaker.Name, speaker.Characters, percent[index])).ToList();
    }

    private static int CountCharacters(IEnumerable<string> contents) =>
        contents.Sum(content => content.Count(character => !char.IsWhiteSpace(character)));

    private static string DescribeSpan(DateTimeOffset? first, DateTimeOffset? last)
    {
        if (first is null || last is null || first.Value.Year <= 1 || last.Value.Year <= 1) return "—";
        var span = last.Value - first.Value;
        if (span < TimeSpan.Zero) span = -span;
        if (span.TotalMinutes < 1) return "不足 1 分钟";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} 分钟";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} 小时 {(int)span.TotalMinutes % 60} 分钟";
        return $"{(int)span.TotalDays} 天 {(int)span.TotalHours % 24} 小时";
    }

    private static string Format(int value) => value.ToString("N0", System.Globalization.CultureInfo.InvariantCulture);
}

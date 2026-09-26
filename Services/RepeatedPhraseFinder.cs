namespace NativeTavern.Services;

/// <summary>A phrase the model keeps reaching for, and how many separate replies used it.</summary>
public sealed class RepeatedPhrase(string phrase, int messages)
{
    public string Phrase { get; } = phrase;
    public int Messages { get; } = messages;
    public override string ToString() => $"{Phrase} ×{Messages}";
}

/// <summary>
/// Finds the pet phrases a model falls back on. Counting replies rather than occurrences keeps a single
/// purple paragraph from drowning out a tic that runs through the whole story, and only the assistant's
/// own text is scanned because the writer's phrasing is not a model habit.
/// </summary>
public static class RepeatedPhraseFinder
{
    private const int MinLength = 3;
    private const int MaxLength = 6;
    private const int MinimumReplies = 3;
    private const int ReplyLimit = 120;
    private const int CharactersPerReply = 4000;
    private const int PhraseLimit = 12;

    public static IReadOnlyList<RepeatedPhrase> Find(IEnumerable<string> replies)
    {
        var documents = replies
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Reverse()                                     // the newest prose matters most
            .Take(ReplyLimit)
            .Select(Normalize)
            .Where(text => text.Length >= MinLength)
            .ToList();
        if (documents.Count < MinimumReplies) return [];

        var repliesUsing = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < documents.Count; index++)
        {
            foreach (var phrase in DistinctPhrases(documents[index]))
                repliesUsing[phrase] = repliesUsing.TryGetValue(phrase, out var count) ? count + 1 : 1;
        }

        var candidates = repliesUsing
            .Where(entry => entry.Value >= MinimumReplies)
            .Select(entry => (entry.Key, Count: entry.Value))
            .OrderByDescending(x => x.Count)
            .ThenByDescending(x => x.Key.Length)
            .ToList();

        // "嘴角微微上扬" already explains "嘴角微微", so a shorter phrase only earns its own row when it
        // also appears in replies the longer one never touched.
        var accepted = new List<(string Phrase, int Count)>();
        foreach (var candidate in candidates)
        {
            if (accepted.Any(kept => kept.Phrase.Contains(candidate.Key, StringComparison.Ordinal) && kept.Count >= candidate.Count))
                continue;
            accepted.Add(candidate);
            if (accepted.Count >= PhraseLimit) break;
        }
        return accepted.Select(x => new RepeatedPhrase(x.Phrase, x.Count)).ToList();
    }

    private static string Normalize(string text)
    {
        var builder = new System.Text.StringBuilder(Math.Min(text.Length, CharactersPerReply));
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character) || !char.IsLetterOrDigit(character)) continue;
            builder.Append(character);
            if (builder.Length >= CharactersPerReply) break;
        }
        return builder.ToString();
    }

    private static IEnumerable<string> DistinctPhrases(string text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var length = MinLength; length <= MaxLength; length++)
        {
            for (var start = 0; start + length <= text.Length; start++)
            {
                var phrase = text.Substring(start, length);
                if (seen.Add(phrase)) yield return phrase;
            }
        }
    }
}

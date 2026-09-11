using System.Text;

namespace NativeTavern.Helpers;

internal sealed class ProgressiveParagraphBuffer
{
    private const int LongParagraphThreshold = 520;
    private readonly StringBuilder buffer = new();

    public IReadOnlyList<string> Append(string chunk)
    {
        if (!string.IsNullOrEmpty(chunk)) buffer.Append(chunk);
        return Drain(completeOnly: true);
    }

    public IReadOnlyList<string> Flush() => Drain(completeOnly: false);

    private IReadOnlyList<string> Drain(bool completeOnly)
    {
        var result = new List<string>();
        while (buffer.Length > 0)
        {
            var text = buffer.ToString();
            var length = FindParagraphLength(text);
            if (length == 0 && text.Length >= LongParagraphThreshold)
                length = FindSentenceLength(text, LongParagraphThreshold);
            if (length == 0)
            {
                if (!completeOnly)
                {
                    result.Add(text);
                    buffer.Clear();
                }
                break;
            }

            result.Add(text[..length]);
            buffer.Remove(0, length);
        }
        return result;
    }

    private static int FindParagraphLength(string text)
    {
        for (var index = 0; index < text.Length - 1; index++)
        {
            if (text[index] != '\n') continue;
            var next = index + 1;
            if (next < text.Length && text[next] == '\r') next++;
            if (next < text.Length && text[next] == '\n') return next + 1;
        }
        return 0;
    }

    private static int FindSentenceLength(string text, int minimum)
    {
        for (var index = minimum; index < text.Length; index++)
            if (text[index] is '。' or '！' or '？' or '.' or '!' or '?') return index + 1;
        return 0;
    }
}

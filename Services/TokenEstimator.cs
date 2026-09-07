using NativeTavern.Models;

namespace NativeTavern.Services;

public static class TokenEstimator
{
    public static int Estimate(IEnumerable<ChatCompletionMessage> messages) =>
        messages.Sum(message => EstimateText(message.Content) + 4 + message.ImageDataUrls.Count * 85);

    public static int EstimateText(string text) => string.IsNullOrEmpty(text) ? 0 :
        text.Sum(character => character is >= '\u4e00' and <= '\u9fff' ? 1 : 0) +
        (int)Math.Ceiling(text.Count(character => character is < '\u4e00' or > '\u9fff') / 4d);
}

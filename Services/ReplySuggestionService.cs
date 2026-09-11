using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;
using NativeTavern.Providers;

namespace NativeTavern.Services;

public sealed class ReplySuggestionService(
    ChatMessageRepository messageRepository,
    SettingsService settingsService,
    ILLMProvider provider,
    ILogger<ReplySuggestionService> logger)
{
    public async Task<IReadOnlyList<string>> GenerateAsync(ChatSession session, CancellationToken cancellationToken = default)
    {
        var settings = await settingsService.LoadAsync();
        if (!settings.IsConfigured) return [];
        var history = (await messageRepository.GetBySessionAsync(session.Id)).TakeLast(14).ToList();
        var transcript = string.Join("\n", history.Select(message =>
        {
            var content = message.Content.Length > 1600 ? message.Content[..1600] + "…" : message.Content;
            return $"{(message.Role == ChatRole.User ? "用户" : "角色")}: {content}";
        }));
        if (string.IsNullOrWhiteSpace(transcript)) return [];

        var request = new ChatCompletionRequest
        {
            Model = settings.Model,
            Temperature = Math.Clamp(settings.Temperature, 0.45, 0.85),
            TopP = settings.TopP,
            MaxTokens = Math.Clamp(settings.MaxTokens, 180, 420),
            Stream = true,
            Messages =
            [
                new ChatCompletionMessage
                {
                    Role = "system",
                    Content = "根据当前角色扮演剧情，为用户生成恰好三个可直接发送的下一步回复选项。" +
                              "三个选项应方向不同，例如推进剧情、探索信息、表达态度，并且必须贴合上下文。" +
                              "使用用户口吻，每项一到两句，不要替角色行动，不要解释。只输出严格 JSON：" +
                              "{\"suggestions\":[\"选项1\",\"选项2\",\"选项3\"]}"
                },
                new ChatCompletionMessage { Role = "user", Content = transcript }
            ]
        };

        var response = new StringBuilder();
        try
        {
            await foreach (var chunk in provider.StreamAsync(request, cancellationToken)) response.Append(chunk);
            return CompleteSuggestions(ParseResponse(response.ToString()), history);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { return []; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Reply suggestion generation failed for session {SessionId}.", session.Id);
            return CompleteSuggestions([], history);
        }
    }

    internal static IReadOnlyList<string> ParseResponse(string response)
    {
        var start = response.IndexOf('{');
        var end = response.LastIndexOf('}');
        if (start < 0 || end <= start) return [];
        try
        {
            using var json = JsonDocument.Parse(response[start..(end + 1)]);
            if (!json.RootElement.TryGetProperty("suggestions", out var suggestions) || suggestions.ValueKind != JsonValueKind.Array)
                return [];
            return suggestions.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String)
                .Select(x => (x.GetString() ?? string.Empty).Trim())
                .Where(x => x.Length > 0)
                .Select(x => x.Length <= 180 ? x : x[..180])
                .Distinct(StringComparer.CurrentCultureIgnoreCase)
                .Take(3)
                .ToList();
        }
        catch (JsonException) { return []; }
    }

    internal static IReadOnlyList<string> CompleteSuggestions(
        IEnumerable<string> generated,
        IReadOnlyList<ChatMessage> history)
    {
        var latest = history.LastOrDefault(x => x.Role == ChatRole.Assistant)?.Content ?? string.Empty;
        var topic = latest.Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (topic.Length > 28) topic = topic[..28] + "…";
        var fallback = new[]
        {
            string.IsNullOrEmpty(topic) ? "我想再了解一下当前的情况。" : $"关于“{topic}”，我想进一步问清楚。",
            "我先仔细观察周围和对方的反应，再决定下一步。",
            "我愿意顺着当前的情势行动，看看接下来会发生什么。"
        };
        return generated.Concat(fallback)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.CurrentCultureIgnoreCase)
            .Take(3)
            .ToList();
    }
}

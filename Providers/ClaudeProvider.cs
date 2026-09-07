using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NativeTavern.Helpers;
using NativeTavern.Models;
using NativeTavern.Services;

namespace NativeTavern.Providers;

public sealed class ClaudeProvider(
    HttpClient httpClient,
    SettingsService settingsService,
    ILogger<ClaudeProvider> logger) : ILLMProvider
{
    public string Id => "claude";
    public string DisplayName => "Claude (Anthropic)";

    public async Task<IReadOnlyList<ModelInfo>> GetModelsAsync(CancellationToken cancellationToken)
    {
        var resolved = await settingsService.LoadResolvedAsync();
        return await GetModelsAsync(resolved.Settings, resolved.ApiKey, cancellationToken);
    }

    public async Task<IReadOnlyList<ModelInfo>> GetModelsAsync(
        ProviderSettings settings, string apiKey, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var request = new HttpRequestMessage(HttpMethod.Get, settings.BaseUrl.TrimEnd('/') + "/models?limit=1000");
        AddHeaders(request, apiKey);
        try
        {
            using var response = await SendAsync(request, timeout.Token);
            return ParseModels(await response.Content.ReadAsStringAsync(timeout.Token));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        { throw new ProviderException("获取 Claude 模型列表超时。"); }
        catch (JsonException ex)
        { throw new ProviderException("Claude 返回了无法识别的模型列表。", ex); }
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var resolved = await settingsService.LoadResolvedAsync();
        var system = string.Join("\n\n", request.Messages.Where(x => x.Role == "system").Select(x => x.Content));
        var messages = NormalizeMessages(request.Messages.Where(x => x.Role != "system"));
        var payload = new
        {
            model = request.Model,
            system = string.IsNullOrWhiteSpace(system) ? null : system,
            messages,
            temperature = request.Temperature,
            top_p = request.TopP,
            max_tokens = request.MaxTokens,
            stream = true
        };
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, resolved.Settings.BaseUrl.TrimEnd('/') + "/messages")
        {
            Content = new StringContent(JsonSerializer.Serialize(payload, JsonDefaults.Options), Encoding.UTF8, "application/json")
        };
        AddHeaders(httpRequest, resolved.ApiKey);
        using var response = await SendAsync(httpRequest, cancellationToken, HttpCompletionOption.ResponseHeadersRead);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) yield break;
            if (!line.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;
            var data = line[5..].Trim();
            string? chunk = null;
            try
            {
                chunk = ParseStreamingContent(data);
            }
            catch (JsonException ex) { logger.LogWarning(ex, "Ignored malformed Claude SSE payload."); }
            if (!string.IsNullOrEmpty(chunk)) yield return chunk;
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
    {
        _ = await GetModelsAsync(cancellationToken);
        return true;
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken,
        HttpCompletionOption completion = HttpCompletionOption.ResponseContentRead)
    {
        try
        {
            var response = await httpClient.SendAsync(request, completion, cancellationToken);
            if (response.IsSuccessStatusCode) return response;
            var status = response.StatusCode;
            response.Dispose();
            throw new ProviderException(MapStatus(status));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (HttpRequestException ex) { throw new ProviderException("无法连接 Claude API。", ex); }
    }

    private static IReadOnlyList<object> NormalizeMessages(IEnumerable<ChatCompletionMessage> source)
    {
        var result = new List<(string Role, object Content)>();
        foreach (var message in source)
        {
            var role = message.Role == "assistant" ? "assistant" : "user";
            var content = BuildContent(message);
            if (result.Count > 0 && result[^1].Role == role && result[^1].Content is string previous && content is string current)
                result[^1] = (role, previous + "\n\n" + current);
            else result.Add((role, content));
        }
        return result.Select(x => (object)new { role = x.Role, content = x.Content }).ToList();
    }

    private static object BuildContent(ChatCompletionMessage message)
    {
        if (message.ImageDataUrls.Count == 0) return message.Content;
        var content = new List<object> { new { type = "text", text = message.Content } };
        foreach (var url in message.ImageDataUrls)
        {
            var separator = url.IndexOf(",", StringComparison.Ordinal);
            var header = separator < 0 ? string.Empty : url[..separator];
            var data = separator < 0 ? string.Empty : url[(separator + 1)..];
            var mime = header.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                ? header[5..].Split(';')[0] : "image/png";
            content.Add(new { type = "image", source = new { type = "base64", media_type = mime, data } });
        }
        return content;
    }

    private static void AddHeaders(HttpRequestMessage request, string apiKey)
    {
        request.Headers.TryAddWithoutValidation("x-api-key", apiKey);
        request.Headers.TryAddWithoutValidation("anthropic-version", "2023-06-01");
    }

    public static IReadOnlyList<ModelInfo> ParseModels(string jsonText)
    {
        using var json = JsonDocument.Parse(jsonText);
        if (!json.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return [];
        return data.EnumerateArray().Select(item => new ModelInfo(
                item.TryGetProperty("id", out var id) ? id.GetString() ?? "" : "",
                item.TryGetProperty("display_name", out var name) ? name.GetString() : null))
            .Where(x => x.Id.Length > 0).ToList();
    }

    public static string? ParseStreamingContent(string data)
    {
        using var json = JsonDocument.Parse(data);
        var root = json.RootElement;
        if (!root.TryGetProperty("type", out var typeValue)) return null;
        var type = typeValue.GetString();
        if (type == "error")
            throw new ProviderException(root.TryGetProperty("error", out var error) &&
                error.TryGetProperty("message", out var message)
                    ? message.GetString() ?? "Claude 流式响应错误。"
                    : "Claude 流式响应错误。");
        if (type != "content_block_delta" || !root.TryGetProperty("delta", out var delta) ||
            !delta.TryGetProperty("type", out var deltaType) || deltaType.GetString() != "text_delta" ||
            !delta.TryGetProperty("text", out var text)) return null;
        return text.GetString();
    }

    private static string MapStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Claude API Key 无效或没有访问权限。",
        HttpStatusCode.NotFound => "Claude 接口或模型不存在。",
        HttpStatusCode.TooManyRequests => "Claude 请求过于频繁，请稍后重试。",
        _ when (int)status >= 500 => "Claude 服务暂时不可用。",
        _ => $"Claude 连接失败：HTTP {(int)status} {status}"
    };
}

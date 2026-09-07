using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NativeTavern.Helpers;
using NativeTavern.Models;
using NativeTavern.Services;

namespace NativeTavern.Providers;

public sealed class OpenAICompatibleProvider(
    HttpClient httpClient,
    SettingsService settingsService,
    ILogger<OpenAICompatibleProvider> logger) : ILLMProvider
{
    public string Id => "openai-compatible";
    public string DisplayName => "OpenAI Compatible";

    public async IAsyncEnumerable<string> StreamAsync(
        ChatCompletionRequest request,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var resolved = await settingsService.LoadResolvedAsync();
        await foreach (var chunk in StreamAsync(request, resolved.Settings, resolved.ApiKey, cancellationToken))
            yield return chunk;
    }

    public async IAsyncEnumerable<string> StreamAsync(
        ChatCompletionRequest request,
        ProviderSettings settings,
        string apiKey,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        using var message = CreateRequest(settings, apiKey, request);
        logger.LogInformation("Provider streaming request started for model {Model}", request.Model);
        HttpResponseMessage response;
        try
        {
            response = await httpClient.SendAsync(
                message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ProviderException("无法连接模型服务，请检查 Base URL 和网络连接。", ex);
        }

        using (response)
        {
            logger.LogInformation("Provider returned HTTP {StatusCode}", (int)response.StatusCode);
            if (!response.IsSuccessStatusCode)
                throw new ProviderException(MapStatus(response.StatusCode));

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var reader = new StreamReader(stream);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var line = await reader.ReadLineAsync(cancellationToken);
                if (line is null) yield break;
                if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                    continue;
                var data = line[5..].Trim();
                if (data == "[DONE]") yield break;
                string? content;
                try
                {
                    using var json = JsonDocument.Parse(data);
                    content = json.RootElement.GetProperty("choices")[0]
                        .GetProperty("delta").TryGetProperty("content", out var value)
                        ? value.GetString() : null;
                }
                catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException)
                {
                    logger.LogWarning(ex, "Ignored malformed SSE payload.");
                    continue;
                }
                if (!string.IsNullOrEmpty(content)) yield return content;
            }
        }
    }

    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken)
    {
        var resolved = await settingsService.LoadResolvedAsync();
        return await TestConnectionAsync(resolved.Settings, resolved.ApiKey, cancellationToken);
    }

    public async Task<bool> TestConnectionAsync(
        ProviderSettings settings, string apiKey, CancellationToken cancellationToken)
    {
        var payload = new ChatCompletionRequest
        {
            Model = settings.Model,
            Messages = [new ChatCompletionMessage { Role = "user", Content = "Hi" }],
            Temperature = settings.Temperature,
            TopP = settings.TopP,
            MaxTokens = 1,
            Stream = false
        };
        using var request = CreateRequest(settings, apiKey, payload);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        try
        {
            using var response = await httpClient.SendAsync(request, timeout.Token);
            logger.LogInformation("Connection test returned HTTP {StatusCode}", (int)response.StatusCode);
            if (!response.IsSuccessStatusCode) throw new ProviderException(MapStatus(response.StatusCode));
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ProviderException("连接测试超时，请检查服务地址和网络。");
        }
        catch (HttpRequestException ex)
        {
            throw new ProviderException("无法连接模型服务，请检查 Base URL 和网络连接。", ex);
        }
    }

    private static HttpRequestMessage CreateRequest(
        ProviderSettings settings, string apiKey, ChatCompletionRequest payload)
    {
        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _))
            throw new ProviderException("Base URL 格式无效。");
        if (string.IsNullOrWhiteSpace(settings.Model))
            throw new ProviderException("请填写模型名称。");
        var url = settings.BaseUrl.TrimEnd('/') + "/chat/completions";
        var message = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(payload, JsonDefaults.Options), Encoding.UTF8, "application/json")
        };
        if (!string.IsNullOrWhiteSpace(apiKey))
            message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        return message;
    }

    private static string MapStatus(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "API Key 无效或没有访问权限。",
        HttpStatusCode.NotFound => "接口或模型不存在，请检查 Base URL 和模型名称。",
        HttpStatusCode.TooManyRequests => "请求过于频繁，请稍后重试。",
        _ when (int)status >= 500 => "模型服务暂时不可用，请稍后重试。",
        _ => $"连接失败：HTTP {(int)status} {status}"
    };
}

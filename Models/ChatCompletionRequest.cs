using System.Text.Json.Serialization;

namespace NativeTavern.Models;

public sealed class ChatCompletionRequest
{
    [JsonPropertyName("model")] public required string Model { get; init; }
    [JsonPropertyName("messages")] public IReadOnlyList<ChatCompletionMessage> Messages { get; init; } = [];
    [JsonPropertyName("temperature")] public double Temperature { get; init; } = 0.8;
    [JsonPropertyName("top_p")] public double TopP { get; init; } = 1.0;
    [JsonPropertyName("max_tokens")] public int MaxTokens { get; init; } = 1024;
    [JsonPropertyName("stream")] public bool Stream { get; init; } = true;
}

public sealed class ChatCompletionMessage
{
    [JsonPropertyName("role")] public required string Role { get; init; }
    [JsonPropertyName("content")] public required string Content { get; init; }
}

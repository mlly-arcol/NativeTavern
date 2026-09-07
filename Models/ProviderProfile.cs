namespace NativeTavern.Models;

public sealed record ProviderProfile(string Id, string DisplayName, string DefaultBaseUrl, bool RequiresApiKey, bool IsLocal = false)
{
    public static IReadOnlyList<ProviderProfile> All { get; } =
    [
        new("openai-compatible", "Custom OpenAI Compatible", "", false),
        new("openai", "OpenAI", "https://api.openai.com/v1", true),
        new("openrouter", "OpenRouter", "https://openrouter.ai/api/v1", true),
        new("deepseek", "DeepSeek", "https://api.deepseek.com/v1", true),
        new("claude", "Claude (Anthropic)", "https://api.anthropic.com/v1", true),
        new("gemini", "Gemini", "https://generativelanguage.googleapis.com/v1beta/openai", true),
        new("ollama", "Ollama", "http://localhost:11434/v1", false, true),
        new("lmstudio", "LM Studio", "http://localhost:1234/v1", false, true),
        new("llamacpp", "llama.cpp server", "http://127.0.0.1:8080/v1", false, true)
    ];
}

public sealed record DetectedProvider(ProviderProfile Profile, IReadOnlyList<ModelInfo> Models)
{
    public string DisplayName => $"{Profile.DisplayName} ({Models.Count} models)";
}

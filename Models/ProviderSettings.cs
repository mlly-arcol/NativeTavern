namespace NativeTavern.Models;

public sealed class ProviderSettings
{
    public string ProviderId { get; set; } = "openai-compatible";
    public string BaseUrl { get; set; } = "https://api.openai.com/v1";
    public string ApiKeyEncrypted { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.8;
    public double TopP { get; set; } = 1.0;
    public int MaxTokens { get; set; } = 1024;
    public bool IncludeCharacterContext { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsConfigured => Uri.TryCreate(BaseUrl, UriKind.Absolute, out _) && !string.IsNullOrWhiteSpace(Model);
}

using System.Text.Json;
using NativeTavern.Data.Repositories;
using NativeTavern.Helpers;
using NativeTavern.Models;
using NativeTavern.Security;

namespace NativeTavern.Services;

public sealed class SettingsService(SettingsRepository repository, ISecretProtector protector)
{
    private const string ProviderSettingsKey = "ProviderSettings";

    public async Task<ProviderSettings> LoadAsync()
    {
        var json = await repository.GetAsync(ProviderSettingsKey);
        if (string.IsNullOrWhiteSpace(json)) return new ProviderSettings();
        return JsonSerializer.Deserialize<ProviderSettings>(json, JsonDefaults.Options) ?? new ProviderSettings();
    }

    public async Task<(ProviderSettings Settings, string ApiKey)> LoadResolvedAsync()
    {
        var settings = await LoadAsync();
        return (settings, protector.Unprotect(settings.ApiKeyEncrypted));
    }

    public async Task SaveAsync(ProviderSettings settings, string? apiKey, bool clearApiKey = false)
    {
        var existing = await LoadAsync();
        if (clearApiKey)
            settings.ApiKeyEncrypted = string.Empty;
        else if (!string.IsNullOrWhiteSpace(apiKey))
            settings.ApiKeyEncrypted = protector.Protect(apiKey);
        else
            settings.ApiKeyEncrypted = existing.ApiKeyEncrypted;

        await repository.SetAsync(
            ProviderSettingsKey,
            JsonSerializer.Serialize(settings, JsonDefaults.Options));
    }

    public async Task SaveLanguageAsync(string languageCode)
    {
        var settings = await LoadAsync();
        settings.LanguageCode = LocalizationService.Normalize(languageCode);
        await SaveAsync(settings, apiKey: null);
    }
}

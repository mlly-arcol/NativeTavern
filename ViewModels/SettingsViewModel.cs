using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NativeTavern.Models;
using NativeTavern.Providers;
using NativeTavern.Services;

namespace NativeTavern.ViewModels;

public partial class SettingsViewModel(
    SettingsService settingsService,
    OpenAICompatibleProvider provider,
    ILogger<SettingsViewModel> logger) : ObservableObject
{
    [ObservableProperty] private string _baseUrl = "https://api.openai.com/v1";
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private double _temperature = 0.8;
    [ObservableProperty] private double _topP = 1.0;
    [ObservableProperty] private int _maxTokens = 1024;
    [ObservableProperty] private bool _includeCharacterContext;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    private bool _clearApiKey;

    public event Action? Saved;

    public async Task InitializeAsync()
    {
        var resolved = await settingsService.LoadResolvedAsync();
        BaseUrl = resolved.Settings.BaseUrl;
        ApiKey = resolved.ApiKey;
        Model = resolved.Settings.Model;
        Temperature = resolved.Settings.Temperature;
        TopP = resolved.Settings.TopP;
        MaxTokens = resolved.Settings.MaxTokens;
        IncludeCharacterContext = resolved.Settings.IncludeCharacterContext;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (!TryBuildSettings(out var settings)) return;
        IsBusy = true;
        try
        {
            await settingsService.SaveAsync(settings, ApiKey, _clearApiKey);
            _clearApiKey = false;
            StatusMessage = "已保存。";
            Saved?.Invoke();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save provider settings.");
            StatusMessage = "保存失败，请查看日志。";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        if (!TryBuildSettings(out var settings)) return;
        IsBusy = true;
        StatusMessage = "正在测试连接…";
        try
        {
            await provider.TestConnectionAsync(settings, ApiKey, CancellationToken.None);
            StatusMessage = "连接成功。";
        }
        catch (ProviderException ex) { StatusMessage = ex.Message; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected connection test failure.");
            StatusMessage = "连接测试失败，请查看日志。";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void ClearApiKey()
    {
        ApiKey = string.Empty;
        _clearApiKey = true;
        StatusMessage = "API Key 将在下次保存时清除。";
    }

    private bool TryBuildSettings(out ProviderSettings settings)
    {
        settings = new ProviderSettings
        {
            BaseUrl = BaseUrl.Trim(),
            Model = Model.Trim(),
            Temperature = Temperature,
            TopP = TopP,
            MaxTokens = MaxTokens,
            IncludeCharacterContext = IncludeCharacterContext
        };
        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _))
        {
            StatusMessage = "请输入有效的 Base URL。";
            return false;
        }
        if (string.IsNullOrWhiteSpace(settings.Model))
        {
            StatusMessage = "请填写模型名称。";
            return false;
        }
        if (Temperature is < 0 or > 2 || TopP is < 0 or > 1 || MaxTokens <= 0)
        {
            StatusMessage = "生成参数超出有效范围。";
            return false;
        }
        return true;
    }

    partial void OnApiKeyChanged(string value)
    {
        if (!string.IsNullOrEmpty(value)) _clearApiKey = false;
    }
}

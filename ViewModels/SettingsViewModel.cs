using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NativeTavern.Models;
using NativeTavern.Providers;
using NativeTavern.Services;

namespace NativeTavern.ViewModels;

public partial class SettingsViewModel(
    SettingsService settingsService,
    ProviderRouter provider,
    ProviderDiscoveryService discoveryService,
    ILogger<SettingsViewModel> logger) : ObservableObject
{
    public IReadOnlyList<ProviderProfile> ProviderProfiles { get; } = ProviderProfile.All;
    public ObservableCollection<ModelInfo> AvailableModels { get; } = [];
    public ObservableCollection<DetectedProvider> DetectedServices { get; } = [];

    [ObservableProperty] private ProviderProfile? _selectedProvider;
    [ObservableProperty] private DetectedProvider? _selectedDetectedService;
    [ObservableProperty] private string _baseUrl = "https://api.openai.com/v1";
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _model = string.Empty;
    [ObservableProperty] private double _temperature = 0.8;
    [ObservableProperty] private double _topP = 1.0;
    [ObservableProperty] private int _maxTokens = 1024;
    [ObservableProperty] private int _contextLength = 8192;
    [ObservableProperty] private bool _autoScanLocalModels = true;
    [ObservableProperty] private bool _includeCharacterContext;
    [ObservableProperty] private bool _includeKnowledgeContext;
    [ObservableProperty] private bool _includeImageContext;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;
    private bool _clearApiKey;
    private bool _initializing;

    public event Action? Saved;

    public async Task InitializeAsync()
    {
        _initializing = true;
        var resolved = await settingsService.LoadResolvedAsync();
        SelectedProvider = ProviderProfiles.FirstOrDefault(x => x.Id == resolved.Settings.ProviderId) ?? ProviderProfiles[0];
        BaseUrl = resolved.Settings.BaseUrl;
        ApiKey = resolved.ApiKey;
        Model = resolved.Settings.Model;
        Temperature = resolved.Settings.Temperature;
        TopP = resolved.Settings.TopP;
        MaxTokens = resolved.Settings.MaxTokens;
        ContextLength = resolved.Settings.ContextLength;
        AutoScanLocalModels = resolved.Settings.AutoScanLocalModels;
        IncludeCharacterContext = resolved.Settings.IncludeCharacterContext;
        IncludeKnowledgeContext = resolved.Settings.IncludeKnowledgeContext;
        IncludeImageContext = resolved.Settings.IncludeImageContext;
        _initializing = false;
        if (AutoScanLocalModels) await ScanLocalAsync();
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
    private async Task DiscoverModelsAsync()
    {
        if (!TryBuildSettings(out var settings, requireModel: false)) return;
        IsBusy = true;
        StatusMessage = "正在获取模型列表…";
        try
        {
            var models = await provider.GetModelsAsync(settings, ApiKey, CancellationToken.None);
            AvailableModels.Clear();
            foreach (var model in models) AvailableModels.Add(model);
            if (models.Count == 1) Model = models[0].Id;
            StatusMessage = $"发现 {models.Count} 个模型。";
        }
        catch (ProviderException ex) { StatusMessage = ex.Message; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Model discovery failed.");
            StatusMessage = "获取模型列表失败，请查看日志。";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ScanLocalAsync()
    {
        IsBusy = true;
        StatusMessage = "正在扫描本地模型服务…";
        try
        {
            var detected = await discoveryService.ScanLocalAsync(CancellationToken.None);
            DetectedServices.Clear();
            foreach (var service in detected) DetectedServices.Add(service);
            StatusMessage = detected.Count == 0 ? "未检测到本地模型服务。" : $"检测到 {detected.Count} 个本地模型服务。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Local provider scan failed.");
            StatusMessage = "本地扫描失败，请查看日志。";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void UseDetectedService()
    {
        if (SelectedDetectedService is null) return;
        SelectedProvider = SelectedDetectedService.Profile;
        BaseUrl = SelectedDetectedService.Profile.DefaultBaseUrl;
        AvailableModels.Clear();
        foreach (var model in SelectedDetectedService.Models) AvailableModels.Add(model);
        if (AvailableModels.Count > 0) Model = AvailableModels[0].Id;
        StatusMessage = $"已选择 {SelectedDetectedService.Profile.DisplayName}，保存后生效。";
    }

    [RelayCommand]
    private void ClearApiKey()
    {
        ApiKey = string.Empty;
        _clearApiKey = true;
        StatusMessage = "API Key 将在下次保存时清除。";
    }

    private bool TryBuildSettings(out ProviderSettings settings, bool requireModel = true)
    {
        settings = new ProviderSettings
        {
            ProviderId = SelectedProvider?.Id ?? "openai-compatible",
            BaseUrl = BaseUrl.Trim(),
            Model = Model.Trim(),
            Temperature = Temperature,
            TopP = TopP,
            MaxTokens = MaxTokens,
            ContextLength = ContextLength,
            AutoScanLocalModels = AutoScanLocalModels,
            IncludeCharacterContext = IncludeCharacterContext,
            IncludeKnowledgeContext = IncludeKnowledgeContext,
            IncludeImageContext = IncludeImageContext
        };
        if (!Uri.TryCreate(settings.BaseUrl, UriKind.Absolute, out _))
        {
            StatusMessage = "请输入有效的 Base URL。";
            return false;
        }
        if (requireModel && string.IsNullOrWhiteSpace(settings.Model))
        {
            StatusMessage = "请填写模型名称。";
            return false;
        }
        if (SelectedProvider?.RequiresApiKey == true && string.IsNullOrWhiteSpace(ApiKey))
        {
            StatusMessage = $"{SelectedProvider.DisplayName} 需要 API Key。";
            return false;
        }
        if (Temperature is < 0 or > 2 || TopP is < 0 or > 1 || MaxTokens <= 0 || ContextLength <= 0)
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

    partial void OnSelectedProviderChanged(ProviderProfile? value)
    {
        if (_initializing || value is null || string.IsNullOrWhiteSpace(value.DefaultBaseUrl)) return;
        BaseUrl = value.DefaultBaseUrl;
        AvailableModels.Clear();
        StatusMessage = $"已选择 {value.DisplayName}。";
    }
}

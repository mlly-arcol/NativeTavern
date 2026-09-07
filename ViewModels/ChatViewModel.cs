using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NativeTavern.Models;
using NativeTavern.Providers;
using NativeTavern.Services;

namespace NativeTavern.ViewModels;

public partial class ChatViewModel(
    ChatService chatService,
    SettingsService settingsService,
    ILogger<ChatViewModel> logger) : ObservableObject
{
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];

    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _sessionTitle = "New Chat";
    [ObservableProperty] private bool _hasProviderConfiguration;

    private ChatSession? _session;
    private CancellationTokenSource? _generationCancellation;

    public event Action? ConfigureRequested;

    public async Task InitializeAsync()
    {
        var loaded = await chatService.LoadLatestAsync();
        _session = loaded.Session;
        SessionTitle = loaded.Session.Title;
        Messages.Clear();
        foreach (var message in loaded.Messages)
            Messages.Add(new ChatMessageViewModel(message));
        await RefreshConfigurationAsync();
    }

    public async Task RefreshConfigurationAsync()
    {
        HasProviderConfiguration = (await settingsService.LoadAsync()).IsConfigured;
    }

    public async Task StartCharacterChatAsync(Character character)
    {
        if (IsGenerating) return;
        _session = await chatService.CreateSessionAsync(character);
        SessionTitle = _session.Title;
        Messages.Clear();
        foreach (var message in await chatService.GetMessagesAsync(_session.Id))
            Messages.Add(new ChatMessageViewModel(message));
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (_session is null || !CanSend()) return;
        ErrorMessage = null;
        IsGenerating = true;
        var input = InputText;
        InputText = string.Empty;
        _generationCancellation = new CancellationTokenSource();
        ChatMessageViewModel? assistantViewModel = null;
        try
        {
            await chatService.SendAsync(
                _session,
                input,
                async (user, assistant) =>
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Messages.Add(new ChatMessageViewModel(user));
                        assistantViewModel = new ChatMessageViewModel(assistant);
                        Messages.Add(assistantViewModel);
                        SessionTitle = _session.Title;
                    });
                },
                async (_, chunk) =>
                {
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        if (assistantViewModel is not null) assistantViewModel.Content += chunk;
                    });
                },
                _generationCancellation.Token);
        }
        catch (OperationCanceledException) when (_generationCancellation.IsCancellationRequested) { }
        catch (ProviderException ex) { ErrorMessage = ex.Message; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat generation failed.");
            ErrorMessage = "生成失败，请查看日志后重试。";
        }
        finally
        {
            _generationCancellation.Dispose();
            _generationCancellation = null;
            IsGenerating = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _generationCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanCreateChat))]
    private async Task NewChatAsync()
    {
        _session = await chatService.CreateSessionAsync();
        SessionTitle = _session.Title;
        Messages.Clear();
        ErrorMessage = null;
    }

    [RelayCommand]
    private void OpenSettings() => ConfigureRequested?.Invoke();

    private bool CanSend() =>
        !IsGenerating && HasProviderConfiguration && !string.IsNullOrWhiteSpace(InputText);
    private bool CanStop() => IsGenerating;
    private bool CanCreateChat() => !IsGenerating;

    partial void OnInputTextChanged(string value) => SendCommand.NotifyCanExecuteChanged();
    partial void OnIsGeneratingChanged(bool value)
    {
        SendCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        NewChatCommand.NotifyCanExecuteChanged();
    }
    partial void OnHasProviderConfigurationChanged(bool value) => SendCommand.NotifyCanExecuteChanged();
}

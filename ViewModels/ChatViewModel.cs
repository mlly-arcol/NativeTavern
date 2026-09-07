using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NativeTavern.Models;
using NativeTavern.Data.Repositories;
using NativeTavern.Providers;
using NativeTavern.Services;

namespace NativeTavern.ViewModels;

public partial class ChatViewModel(
    ChatService chatService,
    PromptRepository promptRepository,
    SettingsService settingsService,
    TrayService trayService,
    ILogger<ChatViewModel> logger) : ObservableObject
{
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];
    public ObservableCollection<ChatSession> Sessions { get; } = [];
    public ObservableCollection<Persona> Personas { get; } = [];
    public ObservableCollection<Lorebook> Lorebooks { get; } = [];
    public ObservableCollection<PromptPreset> Presets { get; } = [];
    public ObservableCollection<string> PendingImagePaths { get; } = [];

    [ObservableProperty] private string _inputText = string.Empty;
    [ObservableProperty] private bool _isGenerating;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string _sessionTitle = "New Chat";
    [ObservableProperty] private bool _hasProviderConfiguration;
    [ObservableProperty] private ChatSession? _selectedSession;
    [ObservableProperty] private Persona? _selectedPersona;
    [ObservableProperty] private Lorebook? _selectedLorebook;
    [ObservableProperty] private PromptPreset? _selectedPreset;
    [ObservableProperty] private string _authorNote = string.Empty;
    [ObservableProperty] private int _estimatedPromptTokens;
    [ObservableProperty] private string _currentAssistantName = "NativeTavern";
    [ObservableProperty] private string _currentAssistantAvatarPath = string.Empty;

    private ChatSession? _session;
    private CancellationTokenSource? _generationCancellation;
    private bool _suppressSessionSelection;

    public event Action? ConfigureRequested;
    public event Action? PromptInspectorRequested;

    public ChatSession? CurrentSession => _session;

    public async Task InitializeAsync()
    {
        var sessions = await chatService.GetSessionsAsync();
        var session = sessions.FirstOrDefault() ?? await chatService.CreateSessionAsync();
        await RefreshPromptOptionsAsync();
        await RefreshSessionsAsync(session.Id);
        await LoadSessionAsync(session);
        await RefreshConfigurationAsync();
    }

    public async Task RefreshConfigurationAsync() =>
        HasProviderConfiguration = (await settingsService.LoadAsync()).IsConfigured;

    public async Task RefreshPromptOptionsAsync()
    {
        var personaId = SelectedPersona?.Id ?? _session?.PersonaId;
        var lorebookId = SelectedLorebook?.Id ?? _session?.LorebookId;
        var presetId = SelectedPreset?.Id ?? _session?.PromptPresetId;
        Personas.Clear();
        foreach (var item in await promptRepository.GetPersonasAsync()) Personas.Add(item);
        Lorebooks.Clear();
        foreach (var item in await promptRepository.GetLorebooksAsync()) Lorebooks.Add(item);
        Presets.Clear();
        foreach (var item in await promptRepository.GetPresetsAsync()) Presets.Add(item);
        SelectedPersona = Personas.FirstOrDefault(x => x.Id == personaId);
        SelectedLorebook = Lorebooks.FirstOrDefault(x => x.Id == lorebookId);
        SelectedPreset = Presets.FirstOrDefault(x => x.Id == presetId);
    }

    [RelayCommand]
    private async Task ApplyPromptContextAsync()
    {
        if (_session is null || IsGenerating) return;
        await chatService.UpdateSessionPromptAsync(
            _session, SelectedPersona?.Id, SelectedLorebook?.Id, SelectedPreset?.Id, AuthorNote.Trim());
        ErrorMessage = null;
        await RefreshSessionsAsync(_session.Id);
        await RefreshTokenEstimateAsync();
    }

    [RelayCommand]
    private async Task ClearPromptContextAsync()
    {
        SelectedPersona = null;
        SelectedLorebook = null;
        SelectedPreset = null;
        AuthorNote = string.Empty;
        await ApplyPromptContextAsync();
    }

    public async Task StartCharacterChatAsync(Character character)
    {
        if (IsGenerating) return;
        var session = await chatService.CreateSessionAsync(character);
        await RefreshSessionsAsync(session.Id);
        await LoadSessionAsync(session);
        ErrorMessage = null;
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (_session is null || !CanSend()) return;
        ErrorMessage = null;
        var input = InputText;
        var images = PendingImagePaths.ToArray();
        InputText = string.Empty;
        PendingImagePaths.Clear();
        using var cancellation = BeginGeneration();
        ChatMessageViewModel? assistantViewModel = null;
        try
        {
            await chatService.SendAsync(
                _session, input, images,
                async (user, assistant) =>
                {
                    var attachments = await chatService.GetAttachmentsAsync(user.Id);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        Messages.Add(new ChatMessageViewModel(user, attachments: attachments));
                        assistantViewModel = new ChatMessageViewModel(
                            assistant,
                            assistantName: CurrentAssistantName,
                            assistantAvatarPath: CurrentAssistantAvatarPath);
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
                }, cancellation.Token);
            if (assistantViewModel is not null && !string.IsNullOrEmpty(assistantViewModel.Content))
            {
                assistantViewModel.SetSwipeState(0, 1, assistantViewModel.Content);
                trayService.Notify("NativeTavern", "助手回复已完成。");
            }
            await RefreshSessionsAsync(_session.Id);
            await RefreshTokenEstimateAsync();
        }
        catch (ProviderException ex) { ErrorMessage = ex.Message; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Chat generation failed.");
            ErrorMessage = "生成失败，请查看日志后重试。";
        }
        finally
        {
            try
            {
                if (assistantViewModel is not null)
                {
                    var count = await chatService.GetSwipeCountAsync(assistantViewModel.Model);
                    assistantViewModel.SetSwipeState(
                        assistantViewModel.Model.CurrentSwipeIndex, count, assistantViewModel.Content);
                }
            }
            finally { EndGeneration(cancellation); }
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _generationCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanCreateChat))]
    private async Task NewChatAsync()
    {
        var session = await chatService.CreateSessionAsync();
        await RefreshSessionsAsync(session.Id);
        await LoadSessionAsync(session);
        ErrorMessage = null;
    }

    public async Task DeleteCurrentChatAsync()
    {
        if (_session is null || IsGenerating) return;
        var deletedId = _session.Id;
        await chatService.DeleteSessionAsync(deletedId);
        var sessions = await chatService.GetSessionsAsync();
        var next = sessions.FirstOrDefault() ?? await chatService.CreateSessionAsync();
        await RefreshSessionsAsync(next.Id);
        await LoadSessionAsync(next);
        ErrorMessage = null;
    }

    [RelayCommand]
    private void BeginEdit(ChatMessageViewModel? message)
    {
        if (!IsGenerating) message?.BeginEdit();
    }

    [RelayCommand]
    private void CancelEdit(ChatMessageViewModel? message) => message?.CancelEdit();

    [RelayCommand]
    private async Task SaveEditAsync(ChatMessageViewModel? message)
    {
        if (message is null || IsGenerating) return;
        if (string.IsNullOrWhiteSpace(message.EditText))
        {
            ErrorMessage = "消息内容不能为空。";
            return;
        }
        message.FinishEdit();
        await chatService.UpdateMessageAsync(message.Model);
        if (message.IsAssistant) message.SetSwipeState(0, 1, message.Content);
        ErrorMessage = null;
    }

    public async Task DeleteMessageAsync(ChatMessageViewModel message)
    {
        if (IsGenerating) return;
        await chatService.DeleteMessageAsync(message.Model.Id);
        Messages.Remove(message);
        ErrorMessage = null;
    }

    [RelayCommand]
    private async Task SwipeLeftAsync(ChatMessageViewModel? message)
    {
        if (message is null || IsGenerating || !message.CanSwipeLeft) return;
        await SelectSwipeAsync(message, message.Model.CurrentSwipeIndex - 1);
    }

    [RelayCommand]
    private async Task SwipeRightAsync(ChatMessageViewModel? message)
    {
        if (message is null || IsGenerating || !message.IsAssistant) return;
        if (message.Model.CurrentSwipeIndex + 1 < message.SwipeCount)
            await SelectSwipeAsync(message, message.Model.CurrentSwipeIndex + 1);
        else
            await GenerateAlternativeCoreAsync(message);
    }

    [RelayCommand]
    private Task RegenerateAsync(ChatMessageViewModel? message) =>
        message is null ? Task.CompletedTask : GenerateAlternativeCoreAsync(message);

    public Task RegenerateLastAsync()
    {
        var last = Messages.LastOrDefault(x => x.IsAssistant);
        return last is null ? Task.CompletedTask : GenerateAlternativeCoreAsync(last);
    }

    [RelayCommand]
    private void OpenSettings() => ConfigureRequested?.Invoke();

    [RelayCommand]
    private void OpenPromptInspector() => PromptInspectorRequested?.Invoke();

    public void AddImages(IEnumerable<string> paths)
    {
        foreach (var path in paths.Where(AttachmentService.IsSupportedImage).Distinct(StringComparer.OrdinalIgnoreCase))
            if (!PendingImagePaths.Contains(path, StringComparer.OrdinalIgnoreCase)) PendingImagePaths.Add(path);
        SendCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemovePendingImage(string? path)
    {
        if (path is not null) PendingImagePaths.Remove(path);
        SendCommand.NotifyCanExecuteChanged();
    }

    public Task<PromptBuildResult?> GetPromptPreviewAsync() =>
        _session is null ? Task.FromResult<PromptBuildResult?>(null) : GetPreviewAsync(_session);

    private async Task<PromptBuildResult?> GetPreviewAsync(ChatSession session) => await chatService.GetPromptPreviewAsync(session);

    private async Task SelectSwipeAsync(ChatMessageViewModel message, int index)
    {
        try
        {
            var result = await chatService.SelectSwipeAsync(message.Model, index);
            message.SetSwipeState(result.Message.CurrentSwipeIndex, result.SwipeCount, result.Message.Content);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Selecting message swipe failed.");
            ErrorMessage = "切换候选回复失败。";
        }
    }

    private async Task GenerateAlternativeCoreAsync(ChatMessageViewModel message)
    {
        if (_session is null || IsGenerating || !message.IsAssistant) return;
        if (!ReferenceEquals(message, Messages.LastOrDefault()))
        {
            ErrorMessage = "只能为聊天末尾的助手消息生成新候选。";
            return;
        }
        if (!HasProviderConfiguration)
        {
            ErrorMessage = "尚未配置模型 Provider，请先前往 Settings。";
            return;
        }

        ErrorMessage = null;
        using var cancellation = BeginGeneration();
        try
        {
            var result = await chatService.GenerateAlternativeAsync(
                _session, message.Model,
                async content => await Application.Current.Dispatcher.InvokeAsync(() => message.Content = content),
                cancellation.Token);
            message.SetSwipeState(result.Message.CurrentSwipeIndex, result.SwipeCount, result.Message.Content);
            await RefreshSessionsAsync(_session.Id);
        }
        catch (ProviderException ex) { ErrorMessage = ex.Message; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Alternative generation failed.");
            ErrorMessage = "重新生成失败，请查看日志后重试。";
        }
        finally
        {
            try
            {
                var count = await chatService.GetSwipeCountAsync(message.Model);
                message.SetSwipeState(message.Model.CurrentSwipeIndex, count, message.Content);
            }
            finally { EndGeneration(cancellation); }
        }
    }

    private async Task LoadSessionAsync(ChatSession session)
    {
        _session = session;
        var character = session.CharacterId is long characterId
            ? await chatService.GetCharacterAsync(characterId) : null;
        CurrentAssistantName = character?.Name ?? "NativeTavern";
        CurrentAssistantAvatarPath = character?.AvatarPath ?? string.Empty;
        SessionTitle = session.Title;
        SetSelectedSession(session.Id);
        SelectedPersona = Personas.FirstOrDefault(x => x.Id == session.PersonaId);
        SelectedLorebook = Lorebooks.FirstOrDefault(x => x.Id == session.LorebookId);
        SelectedPreset = Presets.FirstOrDefault(x => x.Id == session.PromptPresetId);
        AuthorNote = session.AuthorNote;
        Messages.Clear();
        foreach (var message in await chatService.GetMessagesAsync(session.Id))
            Messages.Add(new ChatMessageViewModel(
                message,
                await chatService.GetSwipeCountAsync(message),
                await chatService.GetAttachmentsAsync(message.Id),
                CurrentAssistantName,
                CurrentAssistantAvatarPath));
        await RefreshTokenEstimateAsync();
    }

    private async Task RefreshSessionsAsync(long selectedId)
    {
        var sessions = await chatService.GetSessionsAsync();
        _suppressSessionSelection = true;
        Sessions.Clear();
        foreach (var session in sessions) Sessions.Add(session);
        SelectedSession = Sessions.FirstOrDefault(x => x.Id == selectedId);
        _suppressSessionSelection = false;
    }

    private void SetSelectedSession(long id)
    {
        _suppressSessionSelection = true;
        SelectedSession = Sessions.FirstOrDefault(x => x.Id == id) ?? _session;
        _suppressSessionSelection = false;
    }

    private CancellationTokenSource BeginGeneration()
    {
        IsGenerating = true;
        _generationCancellation = new CancellationTokenSource();
        return _generationCancellation;
    }

    private void EndGeneration(CancellationTokenSource cancellation)
    {
        if (ReferenceEquals(_generationCancellation, cancellation)) _generationCancellation = null;
        IsGenerating = false;
    }

    private bool CanSend() => !IsGenerating && HasProviderConfiguration &&
                              (!string.IsNullOrWhiteSpace(InputText) || PendingImagePaths.Count > 0);
    private bool CanStop() => IsGenerating;
    private bool CanCreateChat() => !IsGenerating;

    partial void OnSelectedSessionChanged(ChatSession? value)
    {
        if (_suppressSessionSelection || value is null || value.Id == _session?.Id) return;
        if (IsGenerating)
        {
            if (_session is not null) SetSelectedSession(_session.Id);
            return;
        }
        _ = LoadSessionSafelyAsync(value);
    }

    private async Task LoadSessionSafelyAsync(ChatSession session)
    {
        try { await LoadSessionAsync(session); ErrorMessage = null; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Loading chat session failed.");
            ErrorMessage = "加载聊天记录失败。";
        }
    }

    private async Task RefreshTokenEstimateAsync()
    {
        try { EstimatedPromptTokens = (await GetPromptPreviewAsync())?.EstimatedTokens ?? 0; }
        catch { EstimatedPromptTokens = 0; }
    }

    partial void OnInputTextChanged(string value) => SendCommand.NotifyCanExecuteChanged();
    partial void OnIsGeneratingChanged(bool value)
    {
        SendCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        NewChatCommand.NotifyCanExecuteChanged();
    }
    partial void OnHasProviderConfigurationChanged(bool value) => SendCommand.NotifyCanExecuteChanged();
}

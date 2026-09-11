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
    CharacterStatusService characterStatusService,
    ReplySuggestionService replySuggestionService,
    PluginService pluginService,
    TrayService trayService,
    ILogger<ChatViewModel> logger) : ObservableObject
{
    public ObservableCollection<ChatMessageViewModel> Messages { get; } = [];
    public ObservableCollection<ChatSession> Sessions { get; } = [];
    public ObservableCollection<Persona> Personas { get; } = [];
    public ObservableCollection<Lorebook> Lorebooks { get; } = [];
    public ObservableCollection<PromptPreset> Presets { get; } = [];
    public ObservableCollection<string> PendingImagePaths { get; } = [];
    public ObservableCollection<Character> GroupMembers { get; } = [];
    public ObservableCollection<string> ReplySuggestions { get; } = [];

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
    [ObservableProperty] private bool _isGroupChat;
    [ObservableProperty] private bool _isBranch;
    [ObservableProperty] private bool _isCharacterStatusPluginEnabled;
    [ObservableProperty] private bool _hasReplySuggestions;
    [ObservableProperty] private bool _isGeneratingSuggestions;

    private ChatSession? _session;
    private CancellationTokenSource? _generationCancellation;
    private CancellationTokenSource? _suggestionCancellation;
    private bool _suppressSessionSelection;
    private long _sessionLoadRequestId;
    private long _suggestionRequestId;

    public event Action? ConfigureRequested;
    public event Action? PromptInspectorRequested;

    public ChatSession? CurrentSession => _session;

    public async Task InitializeAsync()
    {
        pluginService.PluginsChanged += OnPluginsChanged;
        var sessions = await chatService.GetSessionsAsync();
        var session = sessions.FirstOrDefault() ?? await chatService.CreateSessionAsync();
        await RefreshPromptOptionsAsync();
        await RefreshSessionsAsync(session.Id);
        await LoadSessionAsync(session);
        await RefreshConfigurationAsync();
        await RefreshCharacterStatusPluginAsync();
    }

    public async Task RefreshCharacterStatusPluginAsync() =>
        IsCharacterStatusPluginEnabled = await characterStatusService.IsEnabledAsync();

    public async Task<CharacterStatusSnapshot?> GetCharacterStatusAsync(ChatMessageViewModel message)
    {
        if (_session is null || !message.IsAssistant) return null;
        var characterId = message.CharacterId;
        if (characterId is not long id) return null;
        return await characterStatusService.GetAsync(_session.Id, id)
               ?? await characterStatusService.UpdateAsync(_session, id, message.Model.Id);
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

    public async Task UpdatePromptContextAsync(
        Persona? persona,
        Lorebook? lorebook,
        PromptPreset? preset,
        string authorNote)
    {
        SelectedPersona = persona;
        SelectedLorebook = lorebook;
        SelectedPreset = preset;
        AuthorNote = authorNote;
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

    public async Task StartGroupChatAsync(string groupName)
    {
        if (IsGenerating) return;
        try
        {
            var session = await chatService.CreateGroupSessionAsync(groupName);
            await RefreshSessionsAsync(session.Id);
            await LoadSessionAsync(session);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Creating group chat failed.");
            ErrorMessage = ex.Message;
        }
    }

    public Task<IReadOnlyList<Character>> GetAllCharactersAsync() => chatService.GetAllCharactersAsync();

    public async Task ExportCurrentConversationAsync(string path)
    {
        if (_session is null || IsGenerating) return;
        var speakerNames = GroupMembers.ToDictionary(x => x.Id, x => x.Name);
        if (_session.CharacterId is long characterId && !speakerNames.ContainsKey(characterId))
            speakerNames[characterId] = CurrentAssistantName;
        await DataExportService.ExportConversationAsync(
            _session,
            Messages.Select(x => x.Model),
            speakerNames,
            path);
    }

    public async Task RenameCurrentConversationAsync(string title)
    {
        if (_session is null || IsGenerating) return;
        await chatService.UpdateSessionTitleAsync(_session, title);
        SessionTitle = _session.Title;
        await RefreshSessionsAsync(_session.Id);
    }

    [RelayCommand(CanExecute = nameof(CanBranchFromMessage))]
    private async Task BranchFromMessageAsync(ChatMessageViewModel? message)
    {
        if (_session is null || message is null || IsGenerating) return;
        try
        {
            var branch = await chatService.BranchSessionAsync(_session, message.Model.Id);
            await RefreshSessionsAsync(branch.Id);
            await LoadSessionAsync(branch);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Creating conversation branch failed.");
            ErrorMessage = "创建聊天分支失败：" + ex.Message;
        }
    }

    private bool CanBranchFromMessage(ChatMessageViewModel? message) =>
        _session is not null && message is not null && !IsGenerating;

    [RelayCommand(CanExecute = nameof(CanOpenParentConversation))]
    private async Task OpenParentConversationAsync()
    {
        if (_session?.ParentSessionId is not long parentId || IsGenerating) return;
        var parent = await chatService.GetSessionAsync(parentId);
        if (parent is null)
        {
            ErrorMessage = "源对话已被删除。";
            return;
        }
        await LoadSessionAsync(parent);
        ErrorMessage = null;
    }

    private bool CanOpenParentConversation() =>
        _session?.ParentSessionId is not null && !IsGenerating;

    public async Task UpdateGroupChatAsync(string title, IReadOnlyCollection<long> characterIds)
    {
        if (_session is null || !_session.IsGroupChat || IsGenerating) return;
        try
        {
            await chatService.UpdateGroupSessionAsync(_session, title, characterIds);
            await RefreshSessionsAsync(_session.Id);
            await LoadSessionAsync(_session);
            ErrorMessage = null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Updating group chat failed.");
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(CanSend))]
    private async Task SendAsync()
    {
        if (_session is null || !CanSend()) return;
        ErrorMessage = null;
        ClearReplySuggestions();
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
                        var speaker = GroupMembers.FirstOrDefault(x => x.Id == assistant.SpeakerCharacterId);
                        assistantViewModel = new ChatMessageViewModel(
                            assistant,
                            assistantName: speaker?.Name ?? CurrentAssistantName,
                            assistantAvatarPath: speaker?.AvatarPath ?? CurrentAssistantAvatarPath,
                            characterId: assistant.SpeakerCharacterId ?? _session.CharacterId);
                        assistantViewModel.IsStreaming = true;
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
                await RefreshReplySuggestionsAsync(cancellation.Token);
                await UpdateCharacterStatusAsync(assistantViewModel.Model, cancellation.Token);
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
            if (assistantViewModel is not null) assistantViewModel.IsStreaming = false;
            try
            {
                if (assistantViewModel is not null && !string.IsNullOrEmpty(assistantViewModel.Content))
                {
                    var count = await chatService.GetSwipeCountAsync(assistantViewModel.Model);
                    assistantViewModel.SetSwipeState(
                        assistantViewModel.Model.CurrentSwipeIndex, count, assistantViewModel.Content);
                }
                else if (assistantViewModel is not null)
                {
                    Messages.Remove(assistantViewModel);
                }
            }
            finally { EndGeneration(cancellation); }
        }
    }

    [RelayCommand(CanExecute = nameof(CanStop))]
    private void Stop() => _generationCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanGenerateNextSpeaker))]
    private Task NextSpeakerAsync() => GenerateNextSpeakerAsync();

    public async Task GenerateNextSpeakerAsync(Character? requestedSpeaker = null)
    {
        if (_session is null || !CanGenerateNextSpeaker()) return;
        ErrorMessage = null;
        ClearReplySuggestions();
        using var cancellation = BeginGeneration();
        ChatMessageViewModel? assistantViewModel = null;
        try
        {
            await chatService.GenerateNextSpeakerAsync(
                _session,
                requestedSpeaker?.Id,
                async assistant =>
                {
                    var speaker = GroupMembers.FirstOrDefault(x => x.Id == assistant.SpeakerCharacterId);
                    await Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        assistantViewModel = new ChatMessageViewModel(
                            assistant,
                            assistantName: speaker?.Name,
                            assistantAvatarPath: speaker?.AvatarPath,
                            characterId: assistant.SpeakerCharacterId);
                        assistantViewModel.IsStreaming = true;
                        Messages.Add(assistantViewModel);
                    });
                },
                async (_, chunk) => await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (assistantViewModel is not null) assistantViewModel.Content += chunk;
                }),
                cancellation.Token);
            if (assistantViewModel is not null && !string.IsNullOrEmpty(assistantViewModel.Content))
            {
                assistantViewModel.SetSwipeState(0, 1, assistantViewModel.Content);
                await RefreshReplySuggestionsAsync(cancellation.Token);
                await UpdateCharacterStatusAsync(assistantViewModel.Model, cancellation.Token);
            }
            await RefreshSessionsAsync(_session.Id);
            await RefreshTokenEstimateAsync();
        }
        catch (ProviderException ex) { ErrorMessage = ex.Message; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Group chat next speaker generation failed.");
            ErrorMessage = "群聊生成失败，请查看日志后重试。";
        }
        finally
        {
            if (assistantViewModel is not null) assistantViewModel.IsStreaming = false;
            if (assistantViewModel is not null && string.IsNullOrEmpty(assistantViewModel.Content))
                Messages.Remove(assistantViewModel);
            EndGeneration(cancellation);
        }
    }

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
        if (message.IsAssistant)
        {
            message.SetSwipeState(0, 1, message.Content);
            await RefreshReplySuggestionsAsync();
            await UpdateCharacterStatusAsync(message.Model);
        }
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

    [RelayCommand]
    private void SelectReplySuggestion(string? suggestion)
    {
        if (!string.IsNullOrWhiteSpace(suggestion)) InputText = suggestion;
    }

    private async Task RefreshReplySuggestionsAsync(CancellationToken cancellationToken = default)
    {
        var session = _session;
        if (session is null) return;
        _suggestionCancellation?.Cancel();
        var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _suggestionCancellation = linkedCancellation;
        var requestId = Interlocked.Increment(ref _suggestionRequestId);
        ReplySuggestions.Clear();
        HasReplySuggestions = false;
        IsGeneratingSuggestions = true;
        try
        {
            var suggestions = await replySuggestionService.GenerateAsync(session, linkedCancellation.Token);
            if (requestId != Volatile.Read(ref _suggestionRequestId) || _session?.Id != session.Id) return;
            ReplySuggestions.Clear();
            foreach (var suggestion in suggestions) ReplySuggestions.Add(suggestion);
            HasReplySuggestions = ReplySuggestions.Count > 0;
        }
        finally
        {
            if (ReferenceEquals(_suggestionCancellation, linkedCancellation))
            {
                _suggestionCancellation = null;
            }
            linkedCancellation.Dispose();
            if (requestId == Volatile.Read(ref _suggestionRequestId)) IsGeneratingSuggestions = false;
        }
    }

    private void ClearReplySuggestions()
    {
        _suggestionCancellation?.Cancel();
        _suggestionCancellation = null;
        Interlocked.Increment(ref _suggestionRequestId);
        ReplySuggestions.Clear();
        HasReplySuggestions = false;
        IsGeneratingSuggestions = false;
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
            await RefreshReplySuggestionsAsync();
            await UpdateCharacterStatusAsync(message.Model);
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
        message.IsStreaming = true;
        try
        {
            var result = await chatService.GenerateAlternativeAsync(
                _session, message.Model,
                async content => await Application.Current.Dispatcher.InvokeAsync(() => message.Content = content),
                cancellation.Token);
            message.SetSwipeState(result.Message.CurrentSwipeIndex, result.SwipeCount, result.Message.Content);
            await RefreshReplySuggestionsAsync(cancellation.Token);
            await UpdateCharacterStatusAsync(message.Model, cancellation.Token);
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
            message.IsStreaming = false;
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
        var requestId = Interlocked.Increment(ref _sessionLoadRequestId);
        var members = session.IsGroupChat
            ? await chatService.GetGroupMembersAsync(session.Id)
            : [];
        var character = session.CharacterId is long characterId
            ? await chatService.GetCharacterAsync(characterId) : null;
        var assistantName = character?.Name ?? "NativeTavern";
        var assistantAvatarPath = character?.AvatarPath ?? string.Empty;
        var messageViewModels = new List<ChatMessageViewModel>();
        foreach (var message in await chatService.GetMessagesAsync(session.Id))
        {
            var countTask = chatService.GetSwipeCountAsync(message);
            var attachmentsTask = chatService.GetAttachmentsAsync(message.Id);
            await Task.WhenAll(countTask, attachmentsTask);
            var speaker = members.FirstOrDefault(x => x.Id == message.SpeakerCharacterId);
            messageViewModels.Add(new ChatMessageViewModel(
                message,
                await countTask,
                await attachmentsTask,
                speaker?.Name ?? assistantName,
                speaker?.AvatarPath ?? assistantAvatarPath,
                message.SpeakerCharacterId ?? session.CharacterId));
        }

        var estimatedTokens = await GetTokenEstimateAsync(session);
        if (requestId != Volatile.Read(ref _sessionLoadRequestId)) return;

        _session = session;
        ClearReplySuggestions();
        IsGroupChat = session.IsGroupChat;
        IsBranch = session.ParentSessionId is not null;
        GroupMembers.Clear();
        foreach (var member in members) GroupMembers.Add(member);
        CurrentAssistantName = assistantName;
        CurrentAssistantAvatarPath = assistantAvatarPath;
        SessionTitle = session.Title;
        SetSelectedSession(session.Id);
        SelectedPersona = Personas.FirstOrDefault(x => x.Id == session.PersonaId);
        SelectedLorebook = Lorebooks.FirstOrDefault(x => x.Id == session.LorebookId);
        SelectedPreset = Presets.FirstOrDefault(x => x.Id == session.PromptPresetId);
        AuthorNote = session.AuthorNote;
        Messages.Clear();
        foreach (var message in messageViewModels) Messages.Add(message);
        EstimatedPromptTokens = estimatedTokens;
        NextSpeakerCommand.NotifyCanExecuteChanged();
        BranchFromMessageCommand.NotifyCanExecuteChanged();
        OpenParentConversationCommand.NotifyCanExecuteChanged();
        if (Messages.LastOrDefault()?.IsAssistant == true)
            _ = RefreshReplySuggestionsAsync();
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
    private bool CanGenerateNextSpeaker() => !IsGenerating && IsGroupChat &&
                                             HasProviderConfiguration && GroupMembers.Count >= 2;

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
        EstimatedPromptTokens = _session is null ? 0 : await GetTokenEstimateAsync(_session);
    }

    private async Task UpdateCharacterStatusAsync(ChatMessage message, CancellationToken cancellationToken = default)
    {
        if (_session is null || !IsCharacterStatusPluginEnabled) return;
        var characterId = message.SpeakerCharacterId ?? _session.CharacterId;
        if (characterId is long id)
            await characterStatusService.UpdateAsync(_session, id, message.Id, cancellationToken);
    }

    private void OnPluginsChanged() => _ = Application.Current.Dispatcher.InvokeAsync(RefreshCharacterStatusPluginAsync);

    private async Task<int> GetTokenEstimateAsync(ChatSession session)
    {
        try { return (await chatService.GetPromptPreviewAsync(session)).EstimatedTokens; }
        catch { return 0; }
    }

    partial void OnInputTextChanged(string value) => SendCommand.NotifyCanExecuteChanged();
    partial void OnIsGeneratingChanged(bool value)
    {
        SendCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        NewChatCommand.NotifyCanExecuteChanged();
        NextSpeakerCommand.NotifyCanExecuteChanged();
        BranchFromMessageCommand.NotifyCanExecuteChanged();
        OpenParentConversationCommand.NotifyCanExecuteChanged();
    }
    partial void OnHasProviderConfigurationChanged(bool value)
    {
        SendCommand.NotifyCanExecuteChanged();
        NextSpeakerCommand.NotifyCanExecuteChanged();
    }
    partial void OnIsGroupChatChanged(bool value) => NextSpeakerCommand.NotifyCanExecuteChanged();
}

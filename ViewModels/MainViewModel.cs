using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using NativeTavern.Services;

namespace NativeTavern.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly LocalizationService _localizationService;
    public ChatViewModel Chat { get; }
    public SettingsViewModel Settings { get; }
    public CharactersViewModel Characters { get; }
    public PromptStudioViewModel PromptStudio { get; }
    public KnowledgeViewModel Knowledge { get; }
    public PromptInspectorViewModel PromptInspector { get; }

    [ObservableProperty]
    private object _currentViewModel;

    public MainViewModel(ChatViewModel chat, SettingsViewModel settings, CharactersViewModel characters, PromptStudioViewModel promptStudio, KnowledgeViewModel knowledge, PromptInspectorViewModel promptInspector, LocalizationService localizationService)
    {
        Chat = chat;
        Settings = settings;
        Characters = characters;
        PromptStudio = promptStudio;
        Knowledge = knowledge;
        PromptInspector = promptInspector;
        _localizationService = localizationService;
        _currentViewModel = chat;
        chat.ConfigureRequested += ShowSettings;
        settings.Saved += SettingsSaved;
        characters.ChatRequested += StartCharacterChat;
        characters.GroupChatRequested += StartGroupChat;
        promptStudio.Saved += PromptResourcesSaved;
        chat.PromptInspectorRequested += () => _ = ShowPromptInspectorAsync();
    }

    partial void OnCurrentViewModelChanged(object value) => _localizationService.Refresh();

    public async Task InitializeAsync()
    {
        await Settings.InitializeAsync();
        await Chat.InitializeAsync();
        await Characters.InitializeAsync();
        await PromptStudio.InitializeAsync();
        await Knowledge.InitializeAsync();
    }

    [RelayCommand]
    private void ShowChat() => CurrentViewModel = Chat;

    [RelayCommand]
    private void NewChat()
    {
        CurrentViewModel = Chat;
        if (Chat.NewChatCommand.CanExecute(null)) Chat.NewChatCommand.Execute(null);
    }

    [RelayCommand]
    private void ShowSettings() => CurrentViewModel = Settings;

    [RelayCommand]
    private async Task ShowCharactersAsync()
    {
        CurrentViewModel = Characters;
        await Characters.RefreshAsync();
    }

    [RelayCommand]
    private void ShowPromptStudio() => CurrentViewModel = PromptStudio;

    [RelayCommand]
    private void ShowKnowledge() => CurrentViewModel = Knowledge;

    [RelayCommand]
    private async Task ShowPromptInspectorAsync()
    {
        CurrentViewModel = PromptInspector;
        await PromptInspector.RefreshCommand.ExecuteAsync(null);
    }

    private async void StartCharacterChat(Models.Character character)
    {
        await Chat.StartCharacterChatAsync(character);
        CurrentViewModel = Chat;
    }

    private async void StartGroupChat(string groupName)
    {
        await Chat.StartGroupChatAsync(groupName);
        CurrentViewModel = Chat;
    }

    private async void SettingsSaved()
    {
        await Chat.RefreshConfigurationAsync();
        CurrentViewModel = Chat;
    }

    private async void PromptResourcesSaved() => await Chat.RefreshPromptOptionsAsync();

    public async Task HandleDroppedFilesAsync(IEnumerable<string> paths)
    {
        var files = paths.ToArray();
        var documents = files.Where(path => Path.GetExtension(path).ToLowerInvariant() is ".txt" or ".md" or ".markdown" or ".pdf").ToArray();
        if (documents.Length > 0)
        {
            await Knowledge.ImportFilesAsync(documents);
            CurrentViewModel = Knowledge;
            return;
        }
        var card = files.FirstOrDefault(path => Path.GetExtension(path).ToLowerInvariant() is ".png" or ".json");
        if (card is not null)
        {
            await Characters.ImportFileAsync(card);
            CurrentViewModel = Characters;
        }
    }
}

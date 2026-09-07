using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NativeTavern.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ChatViewModel Chat { get; }
    public SettingsViewModel Settings { get; }
    public CharactersViewModel Characters { get; }
    public PromptStudioViewModel PromptStudio { get; }

    [ObservableProperty]
    private object _currentViewModel;

    public MainViewModel(ChatViewModel chat, SettingsViewModel settings, CharactersViewModel characters, PromptStudioViewModel promptStudio)
    {
        Chat = chat;
        Settings = settings;
        Characters = characters;
        PromptStudio = promptStudio;
        _currentViewModel = chat;
        chat.ConfigureRequested += ShowSettings;
        settings.Saved += SettingsSaved;
        characters.ChatRequested += StartCharacterChat;
        promptStudio.Saved += PromptResourcesSaved;
    }

    public async Task InitializeAsync()
    {
        await Settings.InitializeAsync();
        await Chat.InitializeAsync();
        await Characters.InitializeAsync();
        await PromptStudio.InitializeAsync();
    }

    [RelayCommand]
    private void ShowChat() => CurrentViewModel = Chat;

    [RelayCommand]
    private void ShowSettings() => CurrentViewModel = Settings;

    [RelayCommand]
    private void ShowCharacters() => CurrentViewModel = Characters;

    [RelayCommand]
    private void ShowPromptStudio() => CurrentViewModel = PromptStudio;

    private async void StartCharacterChat(Models.Character character)
    {
        await Chat.StartCharacterChatAsync(character);
        CurrentViewModel = Chat;
    }

    private async void SettingsSaved()
    {
        await Chat.RefreshConfigurationAsync();
        CurrentViewModel = Chat;
    }

    private async void PromptResourcesSaved() => await Chat.RefreshPromptOptionsAsync();
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NativeTavern.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ChatViewModel Chat { get; }
    public SettingsViewModel Settings { get; }
    public CharactersViewModel Characters { get; }

    [ObservableProperty]
    private object _currentViewModel;

    public MainViewModel(ChatViewModel chat, SettingsViewModel settings, CharactersViewModel characters)
    {
        Chat = chat;
        Settings = settings;
        Characters = characters;
        _currentViewModel = chat;
        chat.ConfigureRequested += ShowSettings;
        settings.Saved += SettingsSaved;
        characters.ChatRequested += StartCharacterChat;
    }

    public async Task InitializeAsync()
    {
        await Settings.InitializeAsync();
        await Chat.InitializeAsync();
        await Characters.InitializeAsync();
    }

    [RelayCommand]
    private void ShowChat() => CurrentViewModel = Chat;

    [RelayCommand]
    private void ShowSettings() => CurrentViewModel = Settings;

    [RelayCommand]
    private void ShowCharacters() => CurrentViewModel = Characters;

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
}

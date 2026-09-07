using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NativeTavern.ViewModels;

public partial class MainViewModel : ObservableObject
{
    public ChatViewModel Chat { get; }
    public SettingsViewModel Settings { get; }

    [ObservableProperty]
    private object _currentViewModel;

    public MainViewModel(ChatViewModel chat, SettingsViewModel settings)
    {
        Chat = chat;
        Settings = settings;
        _currentViewModel = chat;
        chat.ConfigureRequested += ShowSettings;
        settings.Saved += SettingsSaved;
    }

    public async Task InitializeAsync()
    {
        await Settings.InitializeAsync();
        await Chat.InitializeAsync();
    }

    [RelayCommand]
    private void ShowChat() => CurrentViewModel = Chat;

    [RelayCommand]
    private void ShowSettings() => CurrentViewModel = Settings;

    private async void SettingsSaved()
    {
        await Chat.RefreshConfigurationAsync();
        CurrentViewModel = Chat;
    }
}

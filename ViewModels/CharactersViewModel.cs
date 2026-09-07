using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NativeTavern.Models;
using NativeTavern.Services;

namespace NativeTavern.ViewModels;

public partial class CharactersViewModel(
    CharacterService characterService,
    ILogger<CharactersViewModel> logger) : ObservableObject
{
    public ObservableCollection<Character> Characters { get; } = [];

    [ObservableProperty] private Character? _selectedCharacter;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _favoritesOnly;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _personality = string.Empty;
    [ObservableProperty] private string _scenario = string.Empty;
    [ObservableProperty] private string _firstMessage = string.Empty;
    [ObservableProperty] private string _exampleMessages = string.Empty;
    [ObservableProperty] private string _creator = string.Empty;
    [ObservableProperty] private string _tags = string.Empty;
    [ObservableProperty] private string _avatarPath = string.Empty;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

    public event Action<Character>? ChatRequested;

    public Task InitializeAsync() => RefreshAsync();

    public Task RefreshAsync() => RefreshAsync(SelectedCharacter?.Id);

    public async Task ImportFileAsync(string path)
    {
        IsBusy = true;
        try
        {
            var imported = await characterService.ImportAsync(path);
            StatusMessage = $"已导入角色：{imported.Name}";
            await RefreshAsync(imported.Id);
        }
        catch (Exception ex) when (ex is InvalidDataException or System.Text.Json.JsonException or FormatException)
        {
            logger.LogWarning(ex, "Character card import failed.");
            StatusMessage = "导入失败：" + ex.Message;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected character import failure.");
            StatusMessage = "导入失败，请查看日志。";
        }
        finally { IsBusy = false; }
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedCharacter is null) return;
        IsBusy = true;
        try
        {
            await characterService.DeleteAsync(SelectedCharacter);
            NewCharacter();
            StatusMessage = "角色已删除。";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete character.");
            StatusMessage = "删除失败，请查看日志。";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void NewCharacter()
    {
        SelectedCharacter = null;
        Name = Description = Personality = Scenario = FirstMessage = ExampleMessages = Creator = Tags = AvatarPath = string.Empty;
        IsFavorite = false;
        StatusMessage = "正在创建新角色。";
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            StatusMessage = "角色名称不能为空。";
            return;
        }
        IsBusy = true;
        try
        {
            var character = new Character
            {
                Id = SelectedCharacter?.Id ?? 0,
                CreatedAt = SelectedCharacter?.CreatedAt ?? default,
                Name = Name.Trim(),
                Description = Description,
                Personality = Personality,
                Scenario = Scenario,
                FirstMessage = FirstMessage,
                ExampleMessages = ExampleMessages,
                Creator = Creator,
                Tags = Tags,
                IsFavorite = IsFavorite,
                AvatarPath = SelectedCharacter?.AvatarPath ?? string.Empty
            };
            await characterService.SaveAsync(character, AvatarPath);
            StatusMessage = "角色已保存。";
            await RefreshAsync(character.Id);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
        {
            StatusMessage = ex.Message;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to save character.");
            StatusMessage = "保存失败，请查看日志。";
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ToggleFavoriteAsync(Character? character)
    {
        if (character is null) return;
        character.IsFavorite = !character.IsFavorite;
        await characterService.SaveAsync(character);
        await RefreshAsync(character.Id);
    }

    [RelayCommand(CanExecute = nameof(CanStartChat))]
    private void StartChat()
    {
        if (SelectedCharacter is not null) ChatRequested?.Invoke(SelectedCharacter);
    }

    private bool CanStartChat() => SelectedCharacter is not null;

    private async Task RefreshAsync(long? selectId = null)
    {
        var selectedId = selectId ?? SelectedCharacter?.Id;
        var results = await characterService.SearchAsync(SearchText, FavoritesOnly);
        Characters.Clear();
        foreach (var character in results) Characters.Add(character);
        SelectedCharacter = selectedId is null ? null : Characters.FirstOrDefault(x => x.Id == selectedId);
    }

    partial void OnSelectedCharacterChanged(Character? value)
    {
        StartChatCommand.NotifyCanExecuteChanged();
        if (value is null) return;
        Name = value.Name;
        Description = value.Description;
        Personality = value.Personality;
        Scenario = value.Scenario;
        FirstMessage = value.FirstMessage;
        ExampleMessages = value.ExampleMessages;
        Creator = value.Creator;
        Tags = value.Tags;
        AvatarPath = value.AvatarPath;
        IsFavorite = value.IsFavorite;
        StatusMessage = null;
    }

    partial void OnSearchTextChanged(string value) => _ = RefreshAsync();
    partial void OnFavoritesOnlyChanged(bool value) => _ = RefreshAsync();
}

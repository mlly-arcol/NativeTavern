using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NativeTavern.Models;
using NativeTavern.Services;

namespace NativeTavern.ViewModels;

public partial class CharactersViewModel : ObservableObject
{
    private long _refreshRequestId;
    private readonly ICharacterService characterService;
    private readonly ILogger<CharactersViewModel> logger;

    public ObservableCollection<Character> Characters { get; } = [];
    public ObservableCollection<CharacterGroupNode> CharacterGroups { get; } = [];
    public ObservableCollection<Character> UngroupedCharacters { get; } = [];
    public IEnumerable<string> GroupNames => CharacterGroups.Select(x => x.Name);

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
    [ObservableProperty] private string _groupName = string.Empty;
    [ObservableProperty] private string _avatarPath = string.Empty;
    [ObservableProperty] private bool _isFavorite;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _statusMessage;

    public event Action<Character>? ChatRequested;
    public event Action<string>? GroupChatRequested;

    public CharactersViewModel(
        ICharacterService characterService,
        ILogger<CharactersViewModel> logger)
    {
        this.characterService = characterService;
        this.logger = logger;
    }

    public Task InitializeAsync() => RefreshAsync();

    public Task RefreshAsync() => RefreshAsync(SelectedCharacter?.Id);

    public Task<IReadOnlyList<Character>> GetAllCharactersAsync() =>
        characterService.SearchAsync(string.Empty, false);

    public async Task CreateGroupAsync(string name, IReadOnlyCollection<long> characterIds)
    {
        await RunGroupActionAsync(
            () => characterService.CreateGroupAsync(name, characterIds),
            $"已创建分组：{name.Trim()}",
            name.Trim());
    }

    public async Task UpdateGroupAsync(
        string originalName,
        string name,
        IReadOnlyCollection<long> characterIds)
    {
        await RunGroupActionAsync(
            () => characterService.UpdateGroupAsync(originalName, name, characterIds),
            $"已更新分组：{name.Trim()}",
            name.Trim());
    }

    public async Task DeleteGroupAsync(string name)
    {
        await RunGroupActionAsync(
            () => characterService.DeleteGroupAsync(name),
            $"已删除分组：{name}");
    }

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

    public async Task ExportSelectedAsync(string path)
    {
        if (SelectedCharacter is null) return;
        IsBusy = true;
        try
        {
            await DataExportService.ExportCharacterAsync(SelectedCharacter, path);
            StatusMessage = $"角色卡已导出：{Path.GetFileName(path)}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            logger.LogWarning(ex, "Character card export failed.");
            StatusMessage = "导出失败：" + ex.Message;
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void NewCharacter()
    {
        SelectedCharacter = null;
        Name = Description = Personality = Scenario = FirstMessage = ExampleMessages = Creator = Tags = GroupName = AvatarPath = string.Empty;
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
                GroupName = GroupName.Trim(),
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

    [RelayCommand]
    private static void ToggleGroup(CharacterGroupNode? group)
    {
        if (group is not null) group.IsExpanded = !group.IsExpanded;
    }

    [RelayCommand(CanExecute = nameof(CanStartChat))]
    private void StartChat()
    {
        if (SelectedCharacter is not null) ChatRequested?.Invoke(SelectedCharacter);
    }

    private bool CanStartChat() => SelectedCharacter is not null;

    [RelayCommand]
    private void StartGroupChat(CharacterGroupNode? group)
    {
        if (group is null) return;
        if (group.CharacterCount < 2)
        {
            StatusMessage = "群聊至少需要两个角色。";
            return;
        }
        GroupChatRequested?.Invoke(group.Name);
    }

    private async Task RefreshAsync(long? selectId = null)
    {
        var requestId = Interlocked.Increment(ref _refreshRequestId);
        var selectedId = selectId ?? SelectedCharacter?.Id;
        var query = SearchText;
        var favoritesOnly = FavoritesOnly;

        try
        {
            var characterTask = characterService.SearchAsync(query, favoritesOnly);
            var groupTask = characterService.GetGroupsAsync();
            await Task.WhenAll(characterTask, groupTask);
            var results = await characterTask;
            var groups = await groupTask;
            if (requestId != Volatile.Read(ref _refreshRequestId))
            {
                logger.LogDebug(
                    "Discarded stale character refresh {RequestId} for query {Query}.",
                    requestId,
                    query);
                return;
            }

            UpdateBrowser(results, groups);
            Characters.Clear();
            foreach (var character in results) Characters.Add(character);
            SelectedCharacter = Characters.FirstOrDefault(x => x.Id == selectedId)
                                ?? Characters.FirstOrDefault();
            logger.LogInformation(
                "Character refresh {RequestId} loaded {CharacterCount} characters for query {Query} with FavoritesOnly={FavoritesOnly}; selected {SelectedCharacterId}.",
                requestId,
                Characters.Count,
                query,
                favoritesOnly,
                SelectedCharacter?.Id);
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Character refresh {RequestId} failed for query {Query} with FavoritesOnly={FavoritesOnly}.",
                requestId,
                query,
                favoritesOnly);
            if (requestId == Volatile.Read(ref _refreshRequestId))
                StatusMessage = "角色加载失败，请查看日志。";
        }
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
        GroupName = value.GroupName;
        AvatarPath = value.AvatarPath;
        IsFavorite = value.IsFavorite;
        StatusMessage = null;
    }

    partial void OnSearchTextChanged(string value) => _ = RefreshAsync();
    partial void OnFavoritesOnlyChanged(bool value) => _ = RefreshAsync();

    private void UpdateBrowser(
        IReadOnlyList<Character> characters,
        IReadOnlyList<CharacterGroup> groups,
        string? expandGroup = null)
    {
        var expanded = CharacterGroups.Where(x => x.IsExpanded)
            .Select(x => x.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var showEmptyGroups = string.IsNullOrWhiteSpace(SearchText) && !FavoritesOnly;

        CharacterGroups.Clear();
        foreach (var group in groups)
        {
            var members = characters.Where(x =>
                string.Equals(x.GroupName.Trim(), group.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (!showEmptyGroups && members.Count == 0) continue;

            var node = new CharacterGroupNode(group.Name, group.CharacterCount)
            {
                IsExpanded = expanded.Contains(group.Name) ||
                    string.Equals(group.Name, expandGroup, StringComparison.OrdinalIgnoreCase)
            };
            foreach (var member in members) node.Characters.Add(member);
            CharacterGroups.Add(node);
        }

        UngroupedCharacters.Clear();
        foreach (var character in characters.Where(x => string.IsNullOrWhiteSpace(x.GroupName)))
            UngroupedCharacters.Add(character);
        OnPropertyChanged(nameof(GroupNames));
    }

    private async Task RunGroupActionAsync(
        Func<Task> action,
        string successMessage,
        string? expandGroup = null)
    {
        IsBusy = true;
        try
        {
            await action();
            StatusMessage = successMessage;
            await RefreshAsync();
            if (expandGroup is not null)
            {
                var group = CharacterGroups.FirstOrDefault(x =>
                    string.Equals(x.Name, expandGroup, StringComparison.OrdinalIgnoreCase));
                if (group is not null) group.IsExpanded = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Character group operation failed.");
            StatusMessage = ex is InvalidOperationException
                ? ex.Message
                : "分组操作失败，名称可能已存在。";
        }
        finally { IsBusy = false; }
    }
}

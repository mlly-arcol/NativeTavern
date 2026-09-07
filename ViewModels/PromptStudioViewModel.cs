using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NativeTavern.Data.Repositories;
using NativeTavern.Models;

namespace NativeTavern.ViewModels;

public partial class PromptStudioViewModel(
    PromptRepository repository,
    ILogger<PromptStudioViewModel> logger) : ObservableObject
{
    public ObservableCollection<Persona> Personas { get; } = [];
    public ObservableCollection<Lorebook> Lorebooks { get; } = [];
    public ObservableCollection<LoreEntry> LoreEntries { get; } = [];
    public ObservableCollection<PromptPreset> Presets { get; } = [];

    [ObservableProperty] private Persona? _selectedPersona;
    [ObservableProperty] private string _personaName = "";
    [ObservableProperty] private string _personaContent = "";
    [ObservableProperty] private Lorebook? _selectedLorebook;
    [ObservableProperty] private string _lorebookName = "";
    [ObservableProperty] private string _lorebookDescription = "";
    [ObservableProperty] private bool _lorebookEnabled = true;
    [ObservableProperty] private LoreEntry? _selectedLoreEntry;
    [ObservableProperty] private string _entryName = "";
    [ObservableProperty] private string _entryKeywords = "";
    [ObservableProperty] private string _entrySecondaryKeywords = "";
    [ObservableProperty] private string _entryContent = "";
    [ObservableProperty] private int _entryPriority = 100;
    [ObservableProperty] private int _entryDepth = 4;
    [ObservableProperty] private bool _entryEnabled = true;
    [ObservableProperty] private bool _entryConstant;
    [ObservableProperty] private bool _entrySelective;
    [ObservableProperty] private PromptPreset? _selectedPreset;
    [ObservableProperty] private string _presetName = "";
    [ObservableProperty] private string _systemPrompt = "";
    [ObservableProperty] private string _mainPrompt = "";
    [ObservableProperty] private string _presetModel = "";
    [ObservableProperty] private double? _presetTemperature;
    [ObservableProperty] private double? _presetTopP;
    [ObservableProperty] private int? _presetMaxTokens;
    [ObservableProperty] private string? _statusMessage;

    public event Action? Saved;

    public async Task InitializeAsync()
    {
        await RefreshPersonasAsync();
        await RefreshLorebooksAsync();
        await RefreshPresetsAsync();
    }

    [RelayCommand]
    private void NewPersona()
    {
        SelectedPersona = null; PersonaName = PersonaContent = ""; StatusMessage = "新建 Persona";
    }

    [RelayCommand]
    private async Task SavePersonaAsync()
    {
        if (string.IsNullOrWhiteSpace(PersonaName) || string.IsNullOrWhiteSpace(PersonaContent))
        { StatusMessage = "Persona 名称和内容不能为空。"; return; }
        var item = SelectedPersona ?? new Persona();
        item.Name = PersonaName.Trim(); item.Content = PersonaContent.Trim();
        await RunAsync(async () => { await repository.SavePersonaAsync(item); await RefreshPersonasAsync(item.Id); Saved?.Invoke(); }, "Persona 已保存。");
    }

    public Task DeleteSelectedPersonaAsync() => SelectedPersona is null ? Task.CompletedTask :
        RunAsync(async () => { await repository.DeletePersonaAsync(SelectedPersona.Id); NewPersona(); await RefreshPersonasAsync(); Saved?.Invoke(); }, "Persona 已删除。");

    [RelayCommand]
    private void NewLorebook()
    {
        SelectedLorebook = null; LorebookName = LorebookDescription = ""; LorebookEnabled = true;
        LoreEntries.Clear(); NewLoreEntry(); StatusMessage = "新建 Lorebook";
    }

    [RelayCommand]
    private async Task SaveLorebookAsync()
    {
        if (string.IsNullOrWhiteSpace(LorebookName)) { StatusMessage = "Lorebook 名称不能为空。"; return; }
        var item = SelectedLorebook ?? new Lorebook();
        item.Name = LorebookName.Trim(); item.Description = LorebookDescription.Trim(); item.IsEnabled = LorebookEnabled;
        await RunAsync(async () => { await repository.SaveLorebookAsync(item); await RefreshLorebooksAsync(item.Id); Saved?.Invoke(); }, "Lorebook 已保存。");
    }

    public Task DeleteSelectedLorebookAsync() => SelectedLorebook is null ? Task.CompletedTask :
        RunAsync(async () => { await repository.DeleteLorebookAsync(SelectedLorebook.Id); NewLorebook(); await RefreshLorebooksAsync(); Saved?.Invoke(); }, "Lorebook 已删除。");

    [RelayCommand]
    private void NewLoreEntry()
    {
        SelectedLoreEntry = null; EntryName = EntryKeywords = EntrySecondaryKeywords = EntryContent = "";
        EntryPriority = 100; EntryDepth = 4; EntryEnabled = true; EntryConstant = EntrySelective = false;
    }

    [RelayCommand]
    private async Task SaveLoreEntryAsync()
    {
        if (SelectedLorebook is null) { StatusMessage = "请先保存并选择一个 Lorebook。"; return; }
        if (string.IsNullOrWhiteSpace(EntryName) || string.IsNullOrWhiteSpace(EntryContent))
        { StatusMessage = "条目名称和内容不能为空。"; return; }
        var item = SelectedLoreEntry ?? new LoreEntry { LorebookId = SelectedLorebook.Id };
        item.Name=EntryName.Trim(); item.Keywords=EntryKeywords.Trim(); item.SecondaryKeywords=EntrySecondaryKeywords.Trim(); item.Content=EntryContent.Trim();
        item.Priority=EntryPriority; item.Depth=Math.Max(1,EntryDepth); item.IsEnabled=EntryEnabled; item.IsConstant=EntryConstant; item.IsSelective=EntrySelective;
        await RunAsync(async () => { await repository.SaveLoreEntryAsync(item); await RefreshEntriesAsync(SelectedLorebook.Id, item.Id); Saved?.Invoke(); }, "Lore 条目已保存。");
    }

    public Task DeleteSelectedLoreEntryAsync() => SelectedLoreEntry is null || SelectedLorebook is null ? Task.CompletedTask :
        RunAsync(async () => { await repository.DeleteLoreEntryAsync(SelectedLoreEntry.Id); NewLoreEntry(); await RefreshEntriesAsync(SelectedLorebook.Id); Saved?.Invoke(); }, "Lore 条目已删除。");

    [RelayCommand]
    private void NewPreset()
    {
        SelectedPreset=null; PresetName=SystemPrompt=MainPrompt=PresetModel="";
        PresetTemperature=PresetTopP=null; PresetMaxTokens=null; StatusMessage="新建 Prompt Preset";
    }

    [RelayCommand]
    private async Task SavePresetAsync()
    {
        if (string.IsNullOrWhiteSpace(PresetName)) { StatusMessage="Preset 名称不能为空。"; return; }
        var item=SelectedPreset ?? new PromptPreset();
        item.Name=PresetName.Trim(); item.SystemPrompt=SystemPrompt.Trim(); item.MainPrompt=MainPrompt.Trim(); item.Model=PresetModel.Trim();
        item.Temperature=PresetTemperature; item.TopP=PresetTopP; item.MaxTokens=PresetMaxTokens;
        await RunAsync(async () => { await repository.SavePresetAsync(item); await RefreshPresetsAsync(item.Id); Saved?.Invoke(); }, "Preset 已保存。");
    }

    public Task DeleteSelectedPresetAsync() => SelectedPreset is null ? Task.CompletedTask :
        RunAsync(async () => { await repository.DeletePresetAsync(SelectedPreset.Id); NewPreset(); await RefreshPresetsAsync(); Saved?.Invoke(); }, "Preset 已删除。");

    private async Task RefreshPersonasAsync(long? id = null)
    { Personas.Clear(); foreach(var x in await repository.GetPersonasAsync()) Personas.Add(x); if(id is not null) SelectedPersona=Personas.FirstOrDefault(x=>x.Id==id); }
    private async Task RefreshLorebooksAsync(long? id = null)
    { Lorebooks.Clear(); foreach(var x in await repository.GetLorebooksAsync()) Lorebooks.Add(x); if(id is not null) SelectedLorebook=Lorebooks.FirstOrDefault(x=>x.Id==id); }
    private async Task RefreshEntriesAsync(long lorebookId, long? id = null)
    { LoreEntries.Clear(); foreach(var x in await repository.GetLoreEntriesAsync(lorebookId)) LoreEntries.Add(x); if(id is not null) SelectedLoreEntry=LoreEntries.FirstOrDefault(x=>x.Id==id); }
    private async Task RefreshPresetsAsync(long? id = null)
    { Presets.Clear(); foreach(var x in await repository.GetPresetsAsync()) Presets.Add(x); if(id is not null) SelectedPreset=Presets.FirstOrDefault(x=>x.Id==id); }

    private async Task RunAsync(Func<Task> action, string success)
    {
        try { await action(); StatusMessage=success; }
        catch(Exception ex) { logger.LogError(ex,"Prompt Studio operation failed."); StatusMessage="操作失败，请查看日志。"; }
    }

    partial void OnSelectedPersonaChanged(Persona? value) { if(value is null)return; PersonaName=value.Name; PersonaContent=value.Content; StatusMessage=null; }
    partial void OnSelectedLorebookChanged(Lorebook? value)
    {
        if(value is null)return; LorebookName=value.Name; LorebookDescription=value.Description; LorebookEnabled=value.IsEnabled;
        _=RefreshEntriesAsync(value.Id); StatusMessage=null;
    }
    partial void OnSelectedLoreEntryChanged(LoreEntry? value)
    {
        if(value is null)return; EntryName=value.Name; EntryKeywords=value.Keywords; EntrySecondaryKeywords=value.SecondaryKeywords; EntryContent=value.Content;
        EntryPriority=value.Priority; EntryDepth=value.Depth; EntryEnabled=value.IsEnabled; EntryConstant=value.IsConstant; EntrySelective=value.IsSelective;
    }
    partial void OnSelectedPresetChanged(PromptPreset? value)
    {
        if(value is null)return; PresetName=value.Name; SystemPrompt=value.SystemPrompt; MainPrompt=value.MainPrompt; PresetModel=value.Model;
        PresetTemperature=value.Temperature; PresetTopP=value.TopP; PresetMaxTokens=value.MaxTokens; StatusMessage=null;
    }
}

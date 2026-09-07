using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using NativeTavern.Models;
using NativeTavern.Services;

namespace NativeTavern.ViewModels;

public partial class KnowledgeViewModel(KnowledgeService knowledgeService, ILogger<KnowledgeViewModel> logger) : ObservableObject
{
    public ObservableCollection<KnowledgeDocument> Documents { get; } = [];
    [ObservableProperty] private KnowledgeDocument? _selectedDocument;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private bool _isBusy;

    public Task InitializeAsync() => RefreshAsync();

    public async Task ImportFilesAsync(IEnumerable<string> paths)
    {
        IsBusy = true;
        try
        {
            var imported = 0;
            foreach (var path in paths)
            {
                await knowledgeService.ImportAsync(path);
                imported++;
            }
            StatusMessage = imported == 0 ? "未导入任何文档。" : $"已导入 {imported} 个文档。";
            await RefreshAsync();
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException)
        { StatusMessage = ex.Message; }
        catch (Exception ex)
        { logger.LogError(ex, "Knowledge import failed."); StatusMessage = "导入失败，请查看日志。"; }
        finally { IsBusy = false; }
    }

    public async Task DeleteSelectedAsync()
    {
        if (SelectedDocument is null) return;
        await knowledgeService.DeleteAsync(SelectedDocument);
        SelectedDocument = null;
        StatusMessage = "文档已删除。";
        await RefreshAsync();
    }

    public async Task ToggleSelectedAsync()
    {
        if (SelectedDocument is null) return;
        SelectedDocument.IsEnabled = !SelectedDocument.IsEnabled;
        await knowledgeService.SetEnabledAsync(SelectedDocument.Id, SelectedDocument.IsEnabled);
        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var selectedId = SelectedDocument?.Id;
        Documents.Clear();
        foreach (var document in await knowledgeService.GetDocumentsAsync()) Documents.Add(document);
        SelectedDocument = Documents.FirstOrDefault(x => x.Id == selectedId);
    }
}

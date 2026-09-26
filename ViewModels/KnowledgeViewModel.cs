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
        if (IsBusy) return;
        var files = paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        IsBusy = true;
        try
        {
            var imported = 0;
            var failures = new List<string>();
            for (var index = 0; index < files.Length; index++)
            {
                var path = files[index];
                StatusMessage = $"正在导入 {index + 1}/{files.Length}：{Path.GetFileName(path)}";
                try
                {
                    // Keep synchronous PDF extraction and file copying off the UI thread.
                    var document = await Task.Run(() => knowledgeService.ImportAsync(path));
                    Documents.Add(document);
                    imported++;
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Knowledge import failed for {Path}.", path);
                    var reason = ex is InvalidDataException or FileNotFoundException
                        ? ex.Message : "无法读取或保存该文件";
                    failures.Add($"{Path.GetFileName(path)}：{reason}");
                }
            }
            await RefreshAsync();
            StatusMessage = files.Length == 0 ? "未选择文档。" : $"已导入 {imported}/{files.Length} 个文档。";
            if (failures.Count > 0)
                StatusMessage += $" 失败 {failures.Count} 个：" + string.Join("；", failures.Take(3))
                    + (failures.Count > 3 ? "；其余失败详情请查看日志。" : string.Empty);
        }
        catch (Exception ex) when (ex is InvalidDataException or FileNotFoundException)
        { StatusMessage = ex.Message; }
        catch (Exception ex)
        { logger.LogError(ex, "Knowledge import failed."); StatusMessage = "导入失败，请查看日志。"; }
        finally { IsBusy = false; }
    }

    public async Task DeleteSelectedAsync()
    {
        if (IsBusy || SelectedDocument is not { } document) return;
        IsBusy = true;
        try
        {
            await knowledgeService.DeleteAsync(document);
            SelectedDocument = null;
            await RefreshAsync();
            StatusMessage = "文档已删除。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Knowledge deletion failed.");
            StatusMessage = "删除失败，请重试或查看日志。";
        }
        finally { IsBusy = false; }
    }

    public async Task ToggleSelectedAsync()
    {
        if (IsBusy || SelectedDocument is not { } document) return;
        IsBusy = true;
        try
        {
            var enabled = !document.IsEnabled;
            await knowledgeService.SetEnabledAsync(document.Id, enabled);
            await RefreshAsync();
            StatusMessage = enabled ? "文档已启用。" : "文档已停用。";
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Knowledge toggle failed.");
            StatusMessage = "更新失败，请重试或查看日志。";
        }
        finally { IsBusy = false; }
    }

    private async Task RefreshAsync()
    {
        var selectedId = SelectedDocument?.Id;
        var documents = await knowledgeService.GetDocumentsAsync();
        Documents.Clear();
        foreach (var document in documents) Documents.Add(document);
        SelectedDocument = Documents.FirstOrDefault(x => x.Id == selectedId);
    }
}

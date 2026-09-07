using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace NativeTavern.ViewModels;

public partial class PromptInspectorViewModel(ChatViewModel chat) : ObservableObject
{
    [ObservableProperty] private string _promptText = "Open a chat and select Refresh to inspect the current request.";
    [ObservableProperty] private string _activatedLore = "None";
    [ObservableProperty] private int _estimatedTokens;
    [ObservableProperty] private string? _statusMessage;

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            var preview = await chat.GetPromptPreviewAsync();
            if (preview is null) { StatusMessage = "没有可检查的聊天。"; return; }
            EstimatedTokens = preview.EstimatedTokens;
            ActivatedLore = preview.ActivatedLoreEntries.Count == 0 ? "None" : string.Join(", ", preview.ActivatedLoreEntries);
            PromptText = string.Join("\n\n", preview.Messages.Select((message, index) => $"[{index + 1}] {message.Role}\n{message.Content}"));
            StatusMessage = "Prompt 已刷新。";
        }
        catch (Exception ex) { StatusMessage = "无法生成 Prompt 预览：" + ex.Message; }
    }
}

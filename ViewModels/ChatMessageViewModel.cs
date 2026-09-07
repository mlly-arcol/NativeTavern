using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using NativeTavern.Models;

namespace NativeTavern.ViewModels;

public partial class ChatMessageViewModel : ObservableObject
{
    public ChatMessageViewModel(ChatMessage model, int swipeCount = 0, IEnumerable<ChatAttachment>? attachments = null)
    {
        Model = model;
        _content = model.Content;
        _editText = model.Content;
        _swipeCount = swipeCount;
        Attachments = new ObservableCollection<ChatAttachment>(attachments ?? []);
    }

    public ChatMessage Model { get; }
    public ChatRole Role => Model.Role;
    public bool IsAssistant => Role == ChatRole.Assistant;
    public string RoleLabel => Role == ChatRole.User ? "You" : "Assistant";
    public string SwipeDisplay => IsAssistant && SwipeCount > 0
        ? $"{Model.CurrentSwipeIndex + 1} / {SwipeCount}" : string.Empty;
    public bool CanSwipeLeft => IsAssistant && Model.CurrentSwipeIndex > 0;
    public ObservableCollection<ChatAttachment> Attachments { get; }
    public bool HasAttachments => Attachments.Count > 0;

    [ObservableProperty] private string _content;
    [ObservableProperty] private bool _isEditing;
    [ObservableProperty] private string _editText;
    [ObservableProperty] private int _swipeCount;

    public void BeginEdit() { EditText = Content; IsEditing = true; }
    public void CancelEdit() { EditText = Content; IsEditing = false; }
    public void FinishEdit()
    {
        Content = EditText.Trim();
        Model.Content = Content;
        IsEditing = false;
    }

    public void SetSwipeState(int index, int count, string content)
    {
        Model.CurrentSwipeIndex = index;
        Model.Content = content;
        Content = content;
        SwipeCount = count;
        OnPropertyChanged(nameof(SwipeDisplay));
        OnPropertyChanged(nameof(CanSwipeLeft));
    }

    partial void OnContentChanged(string value) => Model.Content = value;
    partial void OnSwipeCountChanged(int value)
    {
        OnPropertyChanged(nameof(SwipeDisplay));
        OnPropertyChanged(nameof(CanSwipeLeft));
    }
}

using CommunityToolkit.Mvvm.ComponentModel;
using NativeTavern.Models;

namespace NativeTavern.ViewModels;

public partial class ChatMessageViewModel(ChatMessage model) : ObservableObject
{
    public ChatMessage Model { get; } = model;
    public ChatRole Role => Model.Role;
    public string RoleLabel => Role == ChatRole.User ? "You" : "Assistant";

    [ObservableProperty]
    private string _content = model.Content;
}

using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using NativeTavern.ViewModels;
namespace NativeTavern.Views;
public partial class ChatView : UserControl
{
    private ChatViewModel? _viewModel;
    public ChatView() { InitializeComponent(); DataContextChanged += (_, _) => AttachViewModel(); }
    private void AttachViewModel()
    {
        if (_viewModel is not null) _viewModel.Messages.CollectionChanged -= MessagesOnCollectionChanged;
        _viewModel = DataContext as ChatViewModel;
        if (_viewModel is null) return;
        _viewModel.Messages.CollectionChanged += MessagesOnCollectionChanged;
        foreach (var message in _viewModel.Messages) message.PropertyChanged += MessageOnPropertyChanged;
    }
    private void MessagesOnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null) foreach (ChatMessageViewModel message in e.NewItems) message.PropertyChanged += MessageOnPropertyChanged;
        ScrollToLatest();
    }
    private void MessageOnPropertyChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(ChatMessageViewModel.Content)) ScrollToLatest(); }
    private void ScrollToLatest() { if (MessageList.Items.Count > 0) MessageList.ScrollIntoView(MessageList.Items[^1]); }
    private void InputBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift) || _viewModel is null) return;
        if (_viewModel.SendCommand.CanExecute(null)) _viewModel.SendCommand.Execute(null);
        e.Handled = true;
    }
}

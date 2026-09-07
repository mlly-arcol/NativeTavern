using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using NativeTavern.ViewModels;

namespace NativeTavern.Views;

public partial class ChatView : UserControl
{
    private ChatViewModel? _viewModel;

    public ChatView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel();
    }

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
        if (e.NewItems is not null)
            foreach (ChatMessageViewModel message in e.NewItems)
                message.PropertyChanged += MessageOnPropertyChanged;
        if (e.OldItems is not null)
            foreach (ChatMessageViewModel message in e.OldItems)
                message.PropertyChanged -= MessageOnPropertyChanged;
        ScrollToLatest();
    }

    private void MessageOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ChatMessageViewModel.Content)) ScrollToLatest();
    }

    private void ScrollToLatest()
    {
        if (MessageList.Items.Count > 0) MessageList.ScrollIntoView(MessageList.Items[^1]);
    }

    private async void DeleteChat_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        if (MessageBox.Show("确定删除当前聊天及其全部消息吗？", "Delete Chat",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            await _viewModel.DeleteCurrentChatAsync();
    }

    private async void DeleteMessage_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Button { Tag: ChatMessageViewModel message }) return;
        if (MessageBox.Show("确定删除这条消息吗？", "Delete Message",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            await _viewModel.DeleteMessageAsync(message);
    }

    private void CopyMessage_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ChatMessageViewModel message } && !string.IsNullOrEmpty(message.Content))
            Clipboard.SetText(message.Content);
    }

    private async void InputBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel is null) return;
        if (e.Key == Key.Escape && _viewModel.StopCommand.CanExecute(null))
        {
            _viewModel.StopCommand.Execute(null);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Enter && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            await _viewModel.RegenerateLastAsync();
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift)) return;
        if (_viewModel.SendCommand.CanExecute(null)) _viewModel.SendCommand.Execute(null);
        e.Handled = true;
    }

    private void AttachImage_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.gif", Multiselect = true };
        if (dialog.ShowDialog() == true) _viewModel?.AddImages(dialog.FileNames);
    }

    private void InputBox_OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files) _viewModel?.AddImages(files);
        e.Handled = true;
    }
}

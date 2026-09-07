using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using NativeTavern.ViewModels;

namespace NativeTavern.Views;

public partial class KnowledgeView : UserControl
{
    public KnowledgeView() => InitializeComponent();
    private KnowledgeViewModel? ViewModel => DataContext as KnowledgeViewModel;
    private async void Import_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Documents|*.txt;*.md;*.markdown;*.pdf", Multiselect = true };
        if (dialog.ShowDialog() == true && ViewModel is not null) await ViewModel.ImportFilesAsync(dialog.FileNames);
    }
    private async void Delete_OnClick(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null && MessageBox.Show("删除选中的知识库文档及索引？", "Knowledge Base", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            await ViewModel.DeleteSelectedAsync();
    }
    private async void Toggle_OnClick(object sender, RoutedEventArgs e) { if (ViewModel is not null) await ViewModel.ToggleSelectedAsync(); }
    private async void KnowledgeView_OnDrop(object sender, DragEventArgs e)
    { if (ViewModel is not null && e.Data.GetData(DataFormats.FileDrop) is string[] files) await ViewModel.ImportFilesAsync(files); }
}

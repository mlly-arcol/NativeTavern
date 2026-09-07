using System.Windows;
using System.Windows.Controls;
using NativeTavern.ViewModels;

namespace NativeTavern.Views;

public partial class PromptStudioView : UserControl
{
    public PromptStudioView() => InitializeComponent();
    private PromptStudioViewModel? ViewModel => DataContext as PromptStudioViewModel;

    private async void DeletePersona_OnClick(object sender, RoutedEventArgs e)
    { if (ViewModel is not null && Confirm("删除选中的 Persona？")) await ViewModel.DeleteSelectedPersonaAsync(); }
    private async void DeleteLorebook_OnClick(object sender, RoutedEventArgs e)
    { if (ViewModel is not null && Confirm("删除 Lorebook 及其全部条目？")) await ViewModel.DeleteSelectedLorebookAsync(); }
    private async void DeleteLoreEntry_OnClick(object sender, RoutedEventArgs e)
    { if (ViewModel is not null && Confirm("删除选中的 Lore 条目？")) await ViewModel.DeleteSelectedLoreEntryAsync(); }
    private async void DeletePreset_OnClick(object sender, RoutedEventArgs e)
    { if (ViewModel is not null && Confirm("删除选中的 Prompt Preset？")) await ViewModel.DeleteSelectedPresetAsync(); }
    private static bool Confirm(string message) => MessageBox.Show(message, "Prompt Studio", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
}

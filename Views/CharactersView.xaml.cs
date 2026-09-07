using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using NativeTavern.ViewModels;

namespace NativeTavern.Views;

public partial class CharactersView : UserControl
{
    public CharactersView() => InitializeComponent();

    private async void Import_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CharactersViewModel viewModel) return;
        var dialog = new OpenFileDialog
        {
            Title = "Import Character Card",
            Filter = "Character cards (*.png;*.json)|*.png;*.json|PNG cards (*.png)|*.png|JSON cards (*.json)|*.json"
        };
        if (dialog.ShowDialog() == true) await viewModel.ImportFileAsync(dialog.FileName);
    }

    private void ChooseAvatar_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CharactersViewModel viewModel) return;
        var dialog = new OpenFileDialog
        {
            Title = "Choose Character Avatar",
            Filter = "Images (*.png;*.jpg;*.jpeg;*.webp)|*.png;*.jpg;*.jpeg;*.webp"
        };
        if (dialog.ShowDialog() == true) viewModel.AvatarPath = dialog.FileName;
    }

    private async void Delete_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CharactersViewModel { SelectedCharacter: not null } viewModel) return;
        var result = MessageBox.Show(
            $"Delete character '{viewModel.SelectedCharacter.Name}'? This cannot be undone.",
            "NativeTavern", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result == MessageBoxResult.Yes) await viewModel.DeleteSelectedAsync();
    }
}

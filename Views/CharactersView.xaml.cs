using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;
using NativeTavern.Services;
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

    private async void CreateGroup_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CharactersViewModel viewModel) return;
        var characters = await viewModel.GetAllCharactersAsync();
        var dialog = CharacterGroupDialog.Show(
            this,
            null,
            characters,
            [],
            viewModel.CharacterGroups.Select(x => x.Name).ToList());
        if (dialog is not null)
            await viewModel.CreateGroupAsync(dialog.GroupName, dialog.SelectedCharacterIds);
    }

    private async void EditGroup_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CharactersViewModel viewModel ||
            sender is not FrameworkElement { DataContext: CharacterGroupNode group }) return;
        var characters = await viewModel.GetAllCharactersAsync();
        var selectedIds = characters.Where(x =>
            string.Equals(x.GroupName, group.Name, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Id).ToList();
        var dialog = CharacterGroupDialog.Show(
            this,
            group.Name,
            characters,
            selectedIds,
            viewModel.CharacterGroups.Select(x => x.Name).ToList());
        if (dialog is not null)
            await viewModel.UpdateGroupAsync(group.Name, dialog.GroupName, dialog.SelectedCharacterIds);
    }

    private async void DeleteGroup_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CharactersViewModel viewModel ||
            sender is not FrameworkElement { DataContext: CharacterGroupNode group }) return;
        if (ConfirmDeleteDialog.Show(
                this,
                "删除分组？",
                $"确定删除“{group.Name}”吗？",
                $"分组中的 {group.CharacterCount} 个角色会变为未分组，角色本身不会被删除。",
                "删除分组"))
            await viewModel.DeleteGroupAsync(group.Name);
    }

    private void StartGroupChat_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is CharactersViewModel viewModel &&
            sender is FrameworkElement { DataContext: CharacterGroupNode group } &&
            viewModel.StartGroupChatCommand.CanExecute(group))
            viewModel.StartGroupChatCommand.Execute(group);
    }

    private async void Delete_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CharactersViewModel { SelectedCharacter: not null } viewModel) return;
        var character = viewModel.SelectedCharacter;
        if (ConfirmDeleteDialog.Show(this, "删除角色？", $"确定删除“{character.Name}”吗？",
                "角色卡及其本地头像会被永久移除。此操作无法撤销。", "删除角色"))
            await viewModel.DeleteSelectedAsync();
    }

    private async void Export_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CharactersViewModel { SelectedCharacter: not null } viewModel) return;
        var dialog = new SaveFileDialog
        {
            Title = "Export Character Card",
            FileName = DataExportService.CreateSafeFileName(viewModel.SelectedCharacter.Name, "character") + ".json",
            DefaultExt = ".json",
            AddExtension = true,
            Filter = "Character Card JSON (*.json)|*.json"
        };
        if (dialog.ShowDialog() == true) await viewModel.ExportSelectedAsync(dialog.FileName);
    }
}

using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using NativeTavern.ViewModels;
namespace NativeTavern.Views;
public partial class SettingsView : UserControl
{
    private SettingsViewModel? _viewModel;
    private bool _syncing;
    public SettingsView() { InitializeComponent(); DataContextChanged += (_, _) => AttachViewModel(); }
    private void AttachViewModel()
    {
        if (_viewModel is not null) _viewModel.PropertyChanged -= ViewModelOnPropertyChanged;
        _viewModel = DataContext as SettingsViewModel;
        if (_viewModel is null) return;
        _viewModel.PropertyChanged += ViewModelOnPropertyChanged;
        SyncPassword();
    }
    private void ViewModelOnPropertyChanged(object? sender, PropertyChangedEventArgs e) { if (e.PropertyName == nameof(SettingsViewModel.ApiKey)) SyncPassword(); }
    private void SyncPassword()
    {
        if (_viewModel is null || ApiKeyBox.Password == _viewModel.ApiKey) return;
        _syncing = true; ApiKeyBox.Password = _viewModel.ApiKey; _syncing = false;
    }
    private void ApiKeyBox_OnPasswordChanged(object sender, RoutedEventArgs e) { if (!_syncing && _viewModel is not null) _viewModel.ApiKey = ApiKeyBox.Password; }

    private async void Backup_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        var dialog = new SaveFileDialog
        {
            Title = "创建 NativeTavern 备份",
            Filter = "NativeTavern backup (*.zip)|*.zip",
            FileName = $"NativeTavern-backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
        };
        if (dialog.ShowDialog() != true) return;
        try { await _viewModel.CreateBackupAsync(dialog.FileName); }
        catch { MessageBox.Show(_viewModel.StatusMessage, "NativeTavern", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void Restore_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null) return;
        var dialog = new OpenFileDialog
        {
            Title = "恢复 NativeTavern 备份",
            Filter = "NativeTavern backup (*.zip)|*.zip"
        };
        if (dialog.ShowDialog() != true) return;
        if (!ConfirmDeleteDialog.Show(this, "恢复备份？", "当前角色、聊天和设置将被备份内容替换。",
                "恢复前会自动创建当前数据的安全备份；完成后应用将关闭。", "恢复并重启")) return;
        try
        {
            var safetyBackup = await _viewModel.RestoreBackupAsync(dialog.FileName);
            MessageBox.Show($"恢复完成。\n\n恢复前备份：\n{safetyBackup}\n\n请重新启动 NativeTavern。",
                "NativeTavern", MessageBoxButton.OK, MessageBoxImage.Information);
            Application.Current.Shutdown();
        }
        catch { MessageBox.Show(_viewModel.StatusMessage, "NativeTavern", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
}

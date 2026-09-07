using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
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
}

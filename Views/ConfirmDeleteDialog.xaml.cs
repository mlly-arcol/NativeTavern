using System.Windows;
using System.Windows.Input;

namespace NativeTavern.Views;

public partial class ConfirmDeleteDialog : Window
{
    private ConfirmDeleteDialog(
        Window? owner,
        string title,
        string message,
        string detail,
        string confirmLabel)
    {
        InitializeComponent();
        Owner = owner;
        DialogTitle.Text = title;
        DialogMessage.Text = message;
        DialogDetail.Text = detail;
        ConfirmButton.Content = confirmLabel;
    }

    public static bool Show(
        DependencyObject source,
        string title,
        string message,
        string detail,
        string confirmLabel = "确认删除")
    {
        var owner = source as Window ?? Window.GetWindow(source);
        return new ConfirmDeleteDialog(owner, title, message, detail, confirmLabel).ShowDialog() == true;
    }

    private void Dialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        CancelButton.Focus();
        Keyboard.Focus(CancelButton);
    }

    private void Dialog_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        DialogResult = false;
        e.Handled = true;
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Confirm_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;
}

using System.Net.Mail;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Automation;

namespace NativeTavern.Views;

public partial class LoginWindow : Window
{
    private bool _syncingPassword;
    private bool _validating;

    public LoginWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => EmailBox.Focus();
        Closed += (_, _) => { PasswordInput.Clear(); VisiblePassword.Clear(); };
    }

    private void Input_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (EmailError is null) return;
        EmailError.Text = "";
        StatusText.Text = "";
    }

    private void Password_OnChanged(object sender, RoutedEventArgs e)
    {
        if (_syncingPassword || VisiblePassword is null) return;
        _syncingPassword = true;
        VisiblePassword.Text = PasswordInput.Password;
        _syncingPassword = false;
        PasswordError.Text = "";
        StatusText.Text = "";
    }

    private void VisiblePassword_OnChanged(object sender, TextChangedEventArgs e)
    {
        if (_syncingPassword || PasswordInput is null) return;
        _syncingPassword = true;
        PasswordInput.Password = VisiblePassword.Text;
        _syncingPassword = false;
        PasswordError.Text = "";
        StatusText.Text = "";
    }

    private void Reveal_OnClick(object sender, RoutedEventArgs e)
    {
        bool reveal = VisiblePassword.Visibility != Visibility.Visible;
        VisiblePassword.Visibility = reveal ? Visibility.Visible : Visibility.Collapsed;
        PasswordInput.Visibility = reveal ? Visibility.Collapsed : Visibility.Visible;
        RevealButton.Content = reveal ? "隐藏" : "显示";
        AutomationProperties.SetName(RevealButton, reveal ? "隐藏密码" : "显示密码");
        if (reveal) { VisiblePassword.Focus(); VisiblePassword.CaretIndex = VisiblePassword.Text.Length; }
        else PasswordInput.Focus();
    }

    private async void Login_OnClick(object sender, RoutedEventArgs e)
    {
        if (_validating) return;
        string email = EmailBox.Text.Trim();
        bool validEmail = MailAddress.TryCreate(email, out var address)
            && string.Equals(address.Address, email, StringComparison.OrdinalIgnoreCase)
            && address.Host.Contains('.') && !email.Any(char.IsWhiteSpace);
        EmailError.Text = email.Length == 0 ? "请输入邮箱地址。" : validEmail ? "" : "请输入有效的邮箱，例如 name@example.com。";
        PasswordError.Text = PasswordInput.Password.Length == 0 ? "请输入密码。" : "";
        StatusText.Text = "";
        if (!validEmail || PasswordInput.Password.Length == 0)
        {
            if (!validEmail) EmailBox.Focus();
            else if (VisiblePassword.Visibility == Visibility.Visible) VisiblePassword.Focus();
            else PasswordInput.Focus();
            return;
        }

        _validating = true;
        LoginButton.IsEnabled = false;
        LoginButton.Content = "正在校验输入…";
        // Render the local validation state; no authentication request is made.
        LoginProgress.Visibility = SystemParameters.ClientAreaAnimation ? Visibility.Visible : Visibility.Collapsed;
        try
        {
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.Background);
            StatusText.Text = "输入格式已通过校验。账号服务尚未开放，暂时无法登录，请点击“先体验”使用本地功能。";
        }
        finally
        {
            _validating = false;
            LoginButton.IsEnabled = true;
            LoginButton.Content = "登录";
            LoginProgress.Visibility = Visibility.Collapsed;
        }
    }

    private void Register_OnClick(object sender, RoutedEventArgs e) =>
        StatusText.Text = "账号注册尚未开放。你可以先体验本地功能，账号服务接入后再注册。";

    private void Forgot_OnClick(object sender, RoutedEventArgs e) =>
        StatusText.Text = "密码找回尚未开放。当前版本没有在线账号服务，也不会保存你的密码。";

    private void Experience_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;
}

using System.Drawing;
using System.Windows.Forms;

namespace NativeTavern.Services;

public sealed class TrayService : IDisposable
{
    private NotifyIcon? _icon;
    public void Initialize(Action showWindow)
    {
        if (_icon is not null) return;
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open NativeTavern", null, (_, _) => showWindow());
        menu.Items.Add("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown());
        _icon = new NotifyIcon { Text = "NativeTavern", Icon = SystemIcons.Application, ContextMenuStrip = menu, Visible = true };
        _icon.DoubleClick += (_, _) => showWindow();
    }
    public void Notify(string title, string message) => _icon?.ShowBalloonTip(5000, title, message, ToolTipIcon.Info);
    public void Dispose() { if (_icon is null) return; _icon.Visible = false; _icon.Dispose(); _icon = null; }
}

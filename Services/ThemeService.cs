using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace NativeTavern.Services;

public enum AppThemeMode
{
    System,
    Light,
    Dark,
}

/// <summary>
/// Restyles the whole application by mutating the shared palette brushes in place,
/// so every StaticResource consumer repaints without re-resolving its references.
/// </summary>
public static class ThemeService
{
    public sealed record ThemeColor(string ResourceKey, string Light, string Dark);

    public static readonly IReadOnlyList<ThemeColor> Palette =
    [
        new("WindowBrush", "#F8F9FD", "#0F1115"),
        new("SidebarBrush", "#F1F3F9", "#14171D"),
        new("PanelBrush", "#FFFFFF", "#1A1E26"),
        new("CardBrush", "#F3F5FA", "#232833"),
        new("HoverBrush", "#E8ECF6", "#2B313D"),
        new("AccentBrush", "#525BD4", "#6E77E9"),
        new("AccentHoverBrush", "#424AB5", "#858EF0"),
        new("TextBrush", "#20263B", "#E9EDF5"),
        new("MutedTextBrush", "#606B82", "#A6AFC0"),
        new("SubtleTextBrush", "#727D92", "#8892A3"),
        new("BorderBrush", "#DFE4EF", "#2F3641"),
        new("DangerBrush", "#C0392B", "#E4695B"),
        new("OnAccentBrush", "#FFFFFF", "#FFFFFF"),
        new("MaskBrush", "#32000000", "#66000000"),
        new("StrongBorderBrush", "#606060", "#9AA3B2"),
        new("NeutralBgBrush", "#F2F4F8", "#262C36"),
        new("NeutralBorderBrush", "#DCDFE6", "#3B4250"),
        new("SuccessTextBrush", "#18864B", "#4FBE83"),
        new("DangerTextBrush", "#B42318", "#F0938A"),
        new("DangerSolidBrush", "#C83C32", "#B33C31"),
        new("DangerStrongTextBrush", "#8D3028", "#F3B4AB"),
        new("DangerSoftBgBrush", "#FDF0EF", "#33201E"),
        new("DangerSoftBorderBrush", "#F1C6C2", "#5C332D"),
        new("WarningTextBrush", "#765F18", "#F2D98C"),
        new("WarningSoftBgBrush", "#FFF7D6", "#33290F"),
        new("WarningSoftBorderBrush", "#F0D98A", "#5E4C1D"),
        new("AccentTintBgBrush", "#F3F3FD", "#23263A"),
        new("AccentTintBorderBrush", "#DDD9F4", "#3B3F63"),
        new("AccentTintTextBrush", "#514B96", "#B7BEF2"),
        new("AccentDeepTextBrush", "#29264F", "#DDE0FA"),
    ];

    private const int DarkModeAttribute = 20;

    static ThemeService()
    {
        EventManager.RegisterClassHandler(typeof(Window), FrameworkElement.LoadedEvent,
            new RoutedEventHandler((sender, _) =>
            {
                if (sender is Window window) ApplyTitleBar(window);
            }));
        SystemEvents.UserPreferenceChanged += (_, _) =>
        {
            if (Mode == AppThemeMode.System) Apply(AppThemeMode.System);
        };
    }

    public static AppThemeMode Mode { get; private set; } = AppThemeMode.System;
    public static bool IsDark { get; private set; }
    public static event Action? ThemeChanged;

    public static void Apply(AppThemeMode mode, bool? systemDark = null)
    {
        Mode = mode;
        IsDark = ResolveIsDark(mode, systemDark ?? IsSystemDark());
        if (Application.Current is not { } app) return;

        foreach (var entry in Palette)
        {
            var color = ParseColor(IsDark ? entry.Dark : entry.Light);
            switch (app.Resources[entry.ResourceKey])
            {
                case SolidColorBrush brush when !brush.IsFrozen:
                    brush.Color = color;
                    break;
                default:
                    app.Resources[entry.ResourceKey] = new SolidColorBrush(color);
                    break;
            }
        }

        foreach (Window window in app.Windows) ApplyTitleBar(window);
        ThemeChanged?.Invoke();
    }

    public static void Apply(string? modeCode) => Apply(ParseMode(modeCode));

    public static AppThemeMode ParseMode(string? modeCode) =>
        modeCode?.Trim().ToLowerInvariant() switch
        {
            "light" => AppThemeMode.Light,
            "dark" => AppThemeMode.Dark,
            _ => AppThemeMode.System,
        };

    public static string ToCode(AppThemeMode mode) => mode.ToString().ToLowerInvariant();

    public static bool ResolveIsDark(AppThemeMode mode, bool systemDark) => mode switch
    {
        AppThemeMode.Dark => true,
        AppThemeMode.Light => false,
        _ => systemDark,
    };

    public static bool IsSystemDark()
    {
        try
        {
            using var key = Registry.CurrentUser
                .OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            return key?.GetValue("AppsUseLightTheme") is int value && value == 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    public static Color ParseColor(string hex)
    {
        var value = hex.StartsWith("#", StringComparison.Ordinal) ? hex[1..] : hex;
        return value.Length switch
        {
            6 => Color.FromArgb(0xFF,
                Convert.ToByte(value[0..2], 16), Convert.ToByte(value[2..4], 16), Convert.ToByte(value[4..6], 16)),
            8 => Color.FromArgb(
                Convert.ToByte(value[0..2], 16), Convert.ToByte(value[2..4], 16),
                Convert.ToByte(value[4..6], 16), Convert.ToByte(value[6..8], 16)),
            _ => throw new FormatException($"'{hex}' is not a 6 or 8 digit hex color."),
        };
    }

    public static void ApplyTitleBar(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        var enabled = IsDark ? 1 : 0;
        try
        {
            _ = DwmSetWindowAttribute(handle, DarkModeAttribute, ref enabled, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            // Non-Windows hosts keep the default title bar.
        }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}

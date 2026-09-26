using System.IO;
using NativeTavern.Services;
using Xunit;

namespace NativeTavern.Tests;

public class ThemeServiceTests
{
    [Fact]
    public void PaletteKeysAreUniqueAndColorsParse()
    {
        var keys = ThemeService.Palette.Select(entry => entry.ResourceKey).ToList();
        Assert.Equal(keys.Count, keys.Distinct(StringComparer.Ordinal).Count());
        foreach (var entry in ThemeService.Palette)
        {
            Assert.EndsWith("Brush", entry.ResourceKey);
            Assert.IsType<System.Windows.Media.Color>(ThemeService.ParseColor(entry.Light));
            Assert.IsType<System.Windows.Media.Color>(ThemeService.ParseColor(entry.Dark));
        }
    }

    [Fact]
    public void EveryPaletteKeyIsDeclaredInAppResources()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        string? appXaml = null;
        while (directory is not null && appXaml is null)
        {
            var candidate = Path.Combine(directory.FullName, "App.xaml");
            if (File.Exists(candidate)) appXaml = candidate;
            directory = directory.Parent;
        }
        if (appXaml is null) return; // Single-file publish has no source tree alongside the binary.

        var markup = File.ReadAllText(appXaml);
        foreach (var entry in ThemeService.Palette)
            Assert.Contains($"x:Key=\"{entry.ResourceKey}\"", markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AppThemeMode.Dark, false, true)]
    [InlineData(AppThemeMode.Dark, true, true)]
    [InlineData(AppThemeMode.Light, true, false)]
    [InlineData(AppThemeMode.Light, false, false)]
    [InlineData(AppThemeMode.System, true, true)]
    [InlineData(AppThemeMode.System, false, false)]
    public void ResolveIsDarkFollowsModeAndSystemPreference(AppThemeMode mode, bool systemDark, bool expected) =>
        Assert.Equal(expected, ThemeService.ResolveIsDark(mode, systemDark));

    [Theory]
    [InlineData("dark", AppThemeMode.Dark)]
    [InlineData("Dark", AppThemeMode.Dark)]
    [InlineData(" light ", AppThemeMode.Light)]
    [InlineData("light", AppThemeMode.Light)]
    [InlineData("system", AppThemeMode.System)]
    [InlineData(null, AppThemeMode.System)]
    [InlineData("", AppThemeMode.System)]
    [InlineData("nonsense", AppThemeMode.System)]
    public void ModeCodesRoundTrip(string? code, AppThemeMode expected)
    {
        var parsed = ThemeService.ParseMode(code);
        Assert.Equal(expected, parsed);
        var canonical = code?.Trim().ToLowerInvariant();
        if (canonical is "light" or "dark" or "system") Assert.Equal(canonical, ThemeService.ToCode(parsed));
    }
}

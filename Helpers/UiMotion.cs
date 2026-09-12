using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace NativeTavern.Helpers;

/// <summary>Short render-only transitions that respect the Windows animation preference.</summary>
public static class UiMotion
{
    public static readonly DependencyProperty EntranceProperty = DependencyProperty.RegisterAttached(
        "Entrance", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, OnEntranceChanged));
    public static bool GetEntrance(DependencyObject value) => (bool)value.GetValue(EntranceProperty);
    public static void SetEntrance(DependencyObject value, bool enabled) => value.SetValue(EntranceProperty, enabled);

    public static readonly DependencyProperty InteractiveProperty = DependencyProperty.RegisterAttached(
        "Interactive", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, OnInteractiveChanged));
    public static bool GetInteractive(DependencyObject value) => (bool)value.GetValue(InteractiveProperty);
    public static void SetInteractive(DependencyObject value, bool enabled) => value.SetValue(InteractiveProperty, enabled);

    public static readonly DependencyProperty TabTransitionProperty = DependencyProperty.RegisterAttached(
        "TabTransition", typeof(bool), typeof(UiMotion), new PropertyMetadata(false, OnTabTransitionChanged));
    public static bool GetTabTransition(DependencyObject value) => (bool)value.GetValue(TabTransitionProperty);
    public static void SetTabTransition(DependencyObject value, bool enabled) => value.SetValue(TabTransitionProperty, enabled);

    private static void OnTabTransitionChanged(DependencyObject value, DependencyPropertyChangedEventArgs args)
    {
        if (value is not TabControl tabs) return;
        tabs.SelectionChanged -= OnTabChanged;
        if ((bool)args.NewValue) tabs.SelectionChanged += OnTabChanged;
    }

    private static void OnTabChanged(object sender, SelectionChangedEventArgs args)
    {
        if (sender is not TabControl tabs || !ReferenceEquals(args.OriginalSource, tabs)
            || !SystemParameters.ClientAreaAnimation) return;
        if (tabs.Template.FindName("PART_SelectedContentHost", tabs) is FrameworkElement content)
            OnLoaded(content, new RoutedEventArgs());
    }

    private static void OnEntranceChanged(DependencyObject value, DependencyPropertyChangedEventArgs args)
    {
        if (value is not FrameworkElement element) return;
        element.Loaded -= OnLoaded;
        element.Unloaded -= OnUnloaded;
        if ((bool)args.NewValue)
        {
            element.Loaded += OnLoaded;
            element.Unloaded += OnUnloaded;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (sender is not FrameworkElement element || !SystemParameters.ClientAreaAnimation) return;
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            FillBehavior = FillBehavior.Stop
        });
    }

    private static void OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (sender is FrameworkElement element) element.BeginAnimation(UIElement.OpacityProperty, null);
    }

    private static void OnInteractiveChanged(DependencyObject value, DependencyPropertyChangedEventArgs args)
    {
        if (value is not FrameworkElement element) return;
        element.MouseEnter -= OnPointerChanged;
        element.MouseLeave -= OnPointerChanged;
        if ((bool)args.NewValue)
        {
            element.RenderTransform = new TranslateTransform();
            element.MouseEnter += OnPointerChanged;
            element.MouseLeave += OnPointerChanged;
        }
    }

    private static void OnPointerChanged(object sender, MouseEventArgs args)
    {
        if (sender is not FrameworkElement element || element.RenderTransform is not TranslateTransform transform) return;
        double target = element.IsMouseOver && element.IsEnabled ? -1 : 0;
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        transform.Y = target;
        if (!SystemParameters.ClientAreaAnimation) { transform.Y = 0; return; }
        transform.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(element.IsMouseOver ? 0 : -1, target, TimeSpan.FromMilliseconds(120))
            { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }, FillBehavior = FillBehavior.Stop });
    }
}

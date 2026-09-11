using System.Windows;
using NativeTavern.Models;

namespace NativeTavern.Views;

public partial class CharacterStatusDialog : Window
{
    public CharacterStatusDialog(Window owner, CharacterStatusSnapshot status)
    {
        InitializeComponent();
        Owner = owner;
        DataContext = status;
        Title = status.CharacterName + " · 角色状态";
    }

    public static void Show(DependencyObject owner, CharacterStatusSnapshot status)
    {
        var window = Window.GetWindow(owner);
        if (window is not null) new CharacterStatusDialog(window, status).ShowDialog();
    }
}

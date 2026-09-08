using System.Windows;
using System.Windows.Input;
using NativeTavern.Models;
using NativeTavern.ViewModels;

namespace NativeTavern.Views;

public partial class ChatPromptSettingsDialog : Window
{
    private ChatPromptSettingsDialog(Window? owner, ChatViewModel viewModel)
    {
        InitializeComponent();
        Owner = owner;
        DataContext = viewModel;
        PersonaBox.SelectedItem = viewModel.SelectedPersona;
        LorebookBox.SelectedItem = viewModel.SelectedLorebook;
        PresetBox.SelectedItem = viewModel.SelectedPreset;
        AuthorNoteBox.Text = viewModel.AuthorNote;
    }

    public Persona? SelectedPersona => PersonaBox.SelectedItem as Persona;
    public Lorebook? SelectedLorebook => LorebookBox.SelectedItem as Lorebook;
    public PromptPreset? SelectedPreset => PresetBox.SelectedItem as PromptPreset;
    public string AuthorNote => AuthorNoteBox.Text.Trim();

    public static ChatPromptSettingsDialog? Show(DependencyObject source, ChatViewModel viewModel)
    {
        var dialog = new ChatPromptSettingsDialog(Window.GetWindow(source), viewModel);
        return dialog.ShowDialog() == true ? dialog : null;
    }

    private void Clear_OnClick(object sender, RoutedEventArgs e)
    {
        PersonaBox.SelectedItem = null;
        LorebookBox.SelectedItem = null;
        PresetBox.SelectedItem = null;
        AuthorNoteBox.Clear();
        DialogResult = true;
    }

    private void Apply_OnClick(object sender, RoutedEventArgs e) => DialogResult = true;

    private void Dialog_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        DialogResult = false;
        e.Handled = true;
    }
}

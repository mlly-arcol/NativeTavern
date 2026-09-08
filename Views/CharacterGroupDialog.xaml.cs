using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using NativeTavern.Models;

namespace NativeTavern.Views;

public partial class CharacterGroupDialog : Window
{
    private readonly IReadOnlyCollection<string> existingNames;
    private readonly string? originalName;
    private readonly List<CharacterSelectionOption> options;
    private readonly bool validateDuplicateName;
    private readonly int minimumSelectionCount;

    private CharacterGroupDialog(
        Window? owner,
        string? originalName,
        IReadOnlyList<Character> characters,
        IReadOnlyCollection<long> selectedCharacterIds,
        IReadOnlyCollection<string> existingNames,
        string? dialogTitle = null,
        string? saveLabel = null,
        bool validateDuplicateName = true,
        int minimumSelectionCount = 0)
    {
        InitializeComponent();
        Owner = owner;
        this.originalName = originalName;
        this.existingNames = existingNames;
        this.validateDuplicateName = validateDuplicateName;
        this.minimumSelectionCount = minimumSelectionCount;
        var selectedIds = selectedCharacterIds.ToHashSet();
        options = characters.Select(x => new CharacterSelectionOption
        {
            Character = x,
            IsSelected = selectedIds.Contains(x.Id)
        }).ToList();
        CharacterList.ItemsSource = options;

        var editing = originalName is not null;
        DialogTitle.Text = dialogTitle ?? (editing ? "编辑分组" : "创建分组");
        SaveButton.Content = saveLabel ?? (editing ? "保存分组" : "创建分组");
        GroupNameBox.Text = originalName ?? string.Empty;
        UpdateSelectionCount();
    }

    public string GroupName => GroupNameBox.Text.Trim();
    public IReadOnlyList<long> SelectedCharacterIds => options
        .Where(x => x.IsSelected).Select(x => x.Character.Id).ToList();

    public static CharacterGroupDialog? Show(
        DependencyObject source,
        string? originalName,
        IReadOnlyList<Character> characters,
        IReadOnlyCollection<long> selectedCharacterIds,
        IReadOnlyCollection<string> existingNames)
    {
        var owner = source as Window ?? Window.GetWindow(source);
        var dialog = new CharacterGroupDialog(
            owner, originalName, characters, selectedCharacterIds, existingNames);
        return dialog.ShowDialog() == true ? dialog : null;
    }

    public static CharacterGroupDialog? ShowGroupChat(
        DependencyObject source,
        string title,
        IReadOnlyList<Character> characters,
        IReadOnlyCollection<long> selectedCharacterIds)
    {
        var owner = source as Window ?? Window.GetWindow(source);
        var dialog = new CharacterGroupDialog(
            owner,
            title,
            characters,
            selectedCharacterIds,
            [],
            "编辑群聊",
            "保存群聊",
            false,
            2);
        dialog.NameLabel.Text = "群聊名称";
        dialog.SelectionLabel.Text = "选择群聊成员";
        return dialog.ShowDialog() == true ? dialog : null;
    }

    private void Dialog_OnLoaded(object sender, RoutedEventArgs e)
    {
        GroupNameBox.Focus();
        GroupNameBox.SelectAll();
    }

    private void Dialog_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        DialogResult = false;
        e.Handled = true;
    }

    private void Selection_OnChanged(object sender, RoutedEventArgs e) => UpdateSelectionCount();

    private void UpdateSelectionCount()
    {
        if (SelectionCount is not null)
            SelectionCount.Text = $"已选择 {options.Count(x => x.IsSelected)} 个角色";
    }

    private void Cancel_OnClick(object sender, RoutedEventArgs e) => DialogResult = false;

    private void Save_OnClick(object sender, RoutedEventArgs e)
    {
        var name = GroupName;
        if (name.Length == 0)
        {
            ValidationMessage.Text = "请输入分组名称。";
            GroupNameBox.Focus();
            return;
        }

        var duplicate = validateDuplicateName && existingNames.Any(x =>
            !string.Equals(x, originalName, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(x, name, StringComparison.OrdinalIgnoreCase));
        if (duplicate)
        {
            ValidationMessage.Text = "已存在同名分组。";
            GroupNameBox.Focus();
            return;
        }

        if (SelectedCharacterIds.Count < minimumSelectionCount)
        {
            ValidationMessage.Text = $"群聊至少需要选择 {minimumSelectionCount} 个角色。";
            return;
        }

        DialogResult = true;
    }

    private sealed class CharacterSelectionOption
    {
        public required Character Character { get; init; }
        public bool IsSelected { get; set; }
    }
}

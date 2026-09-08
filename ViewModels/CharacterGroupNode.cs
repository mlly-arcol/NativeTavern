using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using NativeTavern.Models;

namespace NativeTavern.ViewModels;

public partial class CharacterGroupNode(string name, int characterCount) : ObservableObject
{
    public string Name { get; } = name;
    public int CharacterCount { get; } = characterCount;
    public ObservableCollection<Character> Characters { get; } = [];

    [ObservableProperty] private bool _isExpanded;
}

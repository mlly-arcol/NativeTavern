using CommunityToolkit.Mvvm.ComponentModel;

namespace NativeTavern.ViewModels;

/// <summary>
/// A speaker chip in the conversation header. The "全部" chip carries no character id and clears the filter.
/// </summary>
public sealed partial class SpeakerFilterOption(long? characterId, string label) : ObservableObject
{
    public long? CharacterId { get; } = characterId;
    public string Label { get; } = label;

    [ObservableProperty] private bool _isSelected;
}

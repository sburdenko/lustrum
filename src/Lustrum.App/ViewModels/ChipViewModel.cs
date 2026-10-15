using CommunityToolkit.Mvvm.ComponentModel;

namespace Lustrum.App.ViewModels;

public sealed partial class ChipViewModel(string label, string tag) : ObservableObject
{
    [ObservableProperty]
    private bool _isOn;

    public string Label { get; } = label;

    public string Tag { get; } = tag;
}

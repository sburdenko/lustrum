using CommunityToolkit.Mvvm.ComponentModel;

namespace Lustrum.App.ViewModels;

public sealed partial class ChipViewModel(string label, ChipFilter filter, string? hint = null) : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    private bool _isOn;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsActive))]
    private bool _isAvailable = !filter.NeedsUsage;

    public string Label { get; } = label;

    public ChipFilter Filter { get; } = filter;

    public string? Hint { get; } = hint;

    public bool IsActive => IsOn && IsAvailable;
}

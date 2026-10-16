using Avalonia.Styling;

namespace Lustrum.Screenshots;

internal sealed record Scenario(
    string File,
    ThemeVariant Theme,
    string? Node = null,
    string? Select = null,
    string? SelectSource = null,
    bool Effective = false,
    string? Chip = null,
    int Tab = 0,
    bool OpenSettings = false,
    string? Problem = null,
    bool HidePaths = true)
{
    public static IReadOnlyList<Scenario> Readme { get; } =
    [
        new("overview.png", ThemeVariant.Light, Select: "release-notes", SelectSource: "Global"),
        new("effective.png", ThemeVariant.Light, Node: "storefront", Effective: true, Select: "release-notes", SelectSource: "Global"),
        new("problems.png", ThemeVariant.Light, Node: "Problems", Problem: "broken-yaml"),
        new("content.png", ThemeVariant.Dark, Select: "code-review", Tab: 1, HidePaths: false),
        new("settings.png", ThemeVariant.Dark, Chip: "sb-*", OpenSettings: true),
    ];
}

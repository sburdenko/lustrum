using Avalonia.Styling;
using Lustrum.App.ViewModels;

namespace Lustrum.Screenshots;

internal sealed record Scenario(
    string File,
    ThemeVariant Theme,
    string? Node = null,
    string? Select = null,
    string? SelectSource = null,
    ProjectView View = ProjectView.Own,
    string? Chip = null,
    int Tab = 0,
    bool OpenSettings = false,
    string? Problem = null,
    bool HidePaths = true,
    bool CheckUpdates = false,
    bool OpenCopyMenu = false)
{
    public static IReadOnlyList<Scenario> Readme { get; } =
    [
        new("overview.png", ThemeVariant.Light, Select: "release-notes", SelectSource: "Global"),
        new("overview-dark.png", ThemeVariant.Dark, Select: "release-notes", SelectSource: "Global"),
        new("effective.png", ThemeVariant.Light, Node: "storefront", View: ProjectView.Effective, Select: "release-notes", SelectSource: "Global"),
        new("problems.png", ThemeVariant.Light, Node: "Problems", Problem: "broken-yaml"),
        new("checks.png", ThemeVariant.Light, Node: "storefront", View: ProjectView.Effective, Select: "pdf-forms", SelectSource: "Global"),
        new("content.png", ThemeVariant.Dark, Select: "code-review", Tab: 1, HidePaths: false),
        new("settings.png", ThemeVariant.Dark, Chip: "sb-*", OpenSettings: true),
        new("compare.png", ThemeVariant.Light, Select: "release-notes", SelectSource: "storefront", Tab: 2, HidePaths: false),
        new("missing.png", ThemeVariant.Light, Node: "storefront", View: ProjectView.Missing, Select: "crash-triage"),
        new("git.png", ThemeVariant.Light, Select: "pdf-forms", Chip: "updates", CheckUpdates: true, Tab: 2, HidePaths: false),
        new("visibility.png", ThemeVariant.Light, Node: "storefront", View: ProjectView.Effective, Select: "sql-helper", OpenCopyMenu: true),
    ];
}

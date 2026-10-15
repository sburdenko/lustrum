using CommunityToolkit.Mvvm.ComponentModel;
using Lustrum.Core.Settings;

namespace Lustrum.App.ViewModels;

public sealed partial class SettingsEditorViewModel : ObservableObject
{
    [ObservableProperty]
    private string _ownPrefixesText = string.Empty;

    [ObservableProperty]
    private string _extraRootsText = string.Empty;

    [ObservableProperty]
    private decimal? _searchDepth;

    [ObservableProperty]
    private string _excludedFoldersText = string.Empty;

    [ObservableProperty]
    private string _editorCommand = string.Empty;

    [ObservableProperty]
    private ThemePreference _selectedTheme;

    public IReadOnlyList<ThemePreference> Themes { get; } = Enum.GetValues<ThemePreference>();

    public int MaxSearchDepth => LustrumSettings.MaxSearchDepth;

    public void Load(LustrumSettings settings)
    {
        OwnPrefixesText = string.Join(", ", settings.OwnPrefixes);
        ExtraRootsText = string.Join(Environment.NewLine, settings.ExtraRoots);
        SearchDepth = settings.SearchDepth;
        ExcludedFoldersText = string.Join(", ", settings.ExcludedFolders);
        EditorCommand = settings.EditorCommand;
        SelectedTheme = settings.Theme;
    }

    public LustrumSettings ToSettings() =>
        new LustrumSettings(
            SplitList(OwnPrefixesText),
            ExtraRootsText.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            (int)(SearchDepth ?? LustrumSettings.Default.SearchDepth),
            SplitList(ExcludedFoldersText),
            EditorCommand,
            SelectedTheme).Normalize();

    private static string[] SplitList(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

using Lustrum.Core.Scanning;

namespace Lustrum.Core.Settings;

public sealed record LustrumSettings(
    IReadOnlyList<string> OwnPrefixes,
    IReadOnlyList<string> ExtraRoots,
    int SearchDepth,
    IReadOnlyList<string> ExcludedFolders,
    string EditorCommand,
    ThemePreference Theme)
{
    public const int MaxSearchDepth = 10;

    public static LustrumSettings Default { get; } = new(
        ScanOptions.Default.OwnPrefixes,
        ScanOptions.Default.ExtraRoots,
        ScanOptions.Default.SearchDepth,
        ScanOptions.Default.ExcludedFolders,
        string.Empty,
        ThemePreference.System);

    public ScanOptions ToScanOptions() => new(OwnPrefixes, ExtraRoots, SearchDepth, ExcludedFolders);

    public LustrumSettings Normalize() => this with
    {
        OwnPrefixes = Clean(OwnPrefixes),
        ExtraRoots = Clean(ExtraRoots).Select(ExpandHome).ToList(),
        SearchDepth = Math.Clamp(SearchDepth, 0, MaxSearchDepth),
        ExcludedFolders = Clean(ExcludedFolders),
        EditorCommand = (EditorCommand ?? string.Empty).Trim(),
        Theme = Enum.IsDefined(Theme) ? Theme : ThemePreference.System,
    };

    private static IReadOnlyList<string> Clean(IEnumerable<string?>? values) =>
        (values ?? [])
            .OfType<string>()
            .Select(v => v.Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    private static string ExpandHome(string path)
    {
        if (path != "~" && !path.StartsWith("~/", StringComparison.Ordinal) && !path.StartsWith("~\\", StringComparison.Ordinal))
        {
            return path;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return path.Length <= 2 ? home : Path.Combine(home, path[2..]);
    }
}

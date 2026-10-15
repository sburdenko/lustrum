namespace Lustrum.Core.Scanning;

public sealed record ScanOptions(
    IReadOnlyList<string> OwnPrefixes,
    IReadOnlyList<string> ExtraRoots,
    int SearchDepth,
    IReadOnlyList<string> ExcludedFolders)
{
    public const int DefaultSearchDepth = 4;

    public static ScanOptions Default { get; } = new(
        ["sb-"],
        [],
        DefaultSearchDepth,
        ["node_modules", "Library", "bin", "obj", ".git"]);
}

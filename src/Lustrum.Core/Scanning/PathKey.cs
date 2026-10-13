namespace Lustrum.Core.Scanning;

internal static class PathKey
{
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    public static string Normalize(string path)
    {
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetPathRoot(path);
        return root is { Length: > 0 } && trimmed.Length < root.Length ? root : trimmed;
    }
}

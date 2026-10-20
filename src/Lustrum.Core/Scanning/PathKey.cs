namespace Lustrum.Core.Scanning;

internal static class PathKey
{
    public static StringComparer Comparer { get; } =
        OperatingSystem.IsLinux() ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase;

    public static StringComparison Comparison { get; } =
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

    /// <summary>True when <paramref name="path"/> is <paramref name="folder"/> or lies below it.</summary>
    public static bool IsWithin(string path, string folder)
    {
        var p = Normalize(path);
        var f = Normalize(folder);
        return string.Equals(p, f, Comparison) || p.StartsWith(f + Path.DirectorySeparatorChar, Comparison);
    }

    public static string Normalize(string path)
    {
        var unified = path.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        var trimmed = unified.TrimEnd(Path.DirectorySeparatorChar);
        var root = Path.GetPathRoot(unified);
        return root is { Length: > 0 } && trimmed.Length < root.Length ? root : trimmed;
    }
}

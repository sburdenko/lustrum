namespace Lustrum.Core.Git;

/// <summary>
/// Resolves symbolic links in every segment of a path, so a skills folder linked into a dotfiles checkout is
/// matched against the repository it really lives in.
/// </summary>
public static class RealPath
{
    private const int MaxDepth = 16;

    public static string Resolve(string path) => Resolve(path, 0);

    private static string Resolve(string path, int depth)
    {
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full)!;
        var current = root;
        foreach (var segment in full[root.Length..].Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (segment.Length == 0)
            {
                continue;
            }

            current = Path.Combine(current, segment);
            if (depth < MaxDepth && LinkTarget(current) is { } target)
            {
                current = Resolve(target, depth + 1);
            }
        }

        return current;
    }

    private static string? LinkTarget(string path)
    {
        try
        {
            var info = new DirectoryInfo(path);
            return info.LinkTarget is null ? null : info.ResolveLinkTarget(returnFinalTarget: true)?.FullName;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}

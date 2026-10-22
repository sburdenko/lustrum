namespace Lustrum.Core.Git;

/// <summary>Recognises a linked git worktree and finds the main checkout it belongs to, without starting git.</summary>
public static class GitWorktree
{
    private const string GitDirPrefix = "gitdir:";

    /// <summary>
    /// The main checkout of a linked worktree rooted at <paramref name="folder"/>, or null when the folder is not one.
    /// A linked worktree has a .git file pointing at .git/worktrees/&lt;name&gt; in the main repository, whose
    /// commondir file leads back to that repository's .git folder. Submodules have a .git file too, but no commondir.
    /// </summary>
    public static string? MainCheckout(string folder)
    {
        try
        {
            var marker = Path.Combine(folder, ".git");
            if (!File.Exists(marker))
            {
                return null;
            }

            var line = File.ReadLines(marker).FirstOrDefault(l => l.StartsWith(GitDirPrefix, StringComparison.Ordinal));
            if (line is null)
            {
                return null;
            }

            var gitDir = Path.GetFullPath(Path.Combine(folder, line[GitDirPrefix.Length..].Trim()));
            var commonDirFile = Path.Combine(gitDir, "commondir");
            if (!File.Exists(commonDirFile))
            {
                return null;
            }

            var common = Path.GetFullPath(Path.Combine(gitDir, File.ReadAllText(commonDirFile).Trim()))
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(common) == ".git" ? Path.GetDirectoryName(common) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return null;
        }
    }
}

namespace Lustrum.Core.Git;

/// <summary>Parses <c>git status --porcelain=v1 -z</c>, where a rename or copy is followed by its old path.</summary>
public static class GitStatusParser
{
    private static readonly HashSet<string> ConflictCodes = ["DD", "AU", "UD", "UA", "DU", "AA", "UU"];

    public static IReadOnlyList<GitChange> Parse(string output)
    {
        var entries = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var changes = new List<GitChange>();
        for (var i = 0; i < entries.Length; i++)
        {
            var entry = entries[i];
            if (entry.Length < 4)
            {
                continue;
            }

            var code = entry[..2];
            changes.Add(new GitChange(entry[3..], KindOf(code)));
            if (code.Contains('R') || code.Contains('C'))
            {
                i++;
            }
        }

        return changes;
    }

    private static GitChangeKind KindOf(string code)
    {
        if (code == "??")
        {
            return GitChangeKind.Untracked;
        }

        if (ConflictCodes.Contains(code))
        {
            return GitChangeKind.Conflicted;
        }

        if (code.Contains('R') || code.Contains('C'))
        {
            return GitChangeKind.Renamed;
        }

        if (code.Contains('D'))
        {
            return GitChangeKind.Deleted;
        }

        return code.Contains('A') ? GitChangeKind.Added : GitChangeKind.Modified;
    }
}

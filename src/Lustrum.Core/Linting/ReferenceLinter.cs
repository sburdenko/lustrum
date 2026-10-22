using System.Text.RegularExpressions;

namespace Lustrum.Core.Linting;

/// <summary>
/// Finds links from a skill to files in its own folder that are not there. Markdown links count, inline and
/// reference style, and so do inline code paths under scripts/, references/ and assets/, or under another resource
/// folder the skill has. Fenced code, URLs, absolute paths, placeholders and paths that leave the skill folder are not
/// checked, to keep false alarms out.
/// </summary>
public static partial class ReferenceLinter
{
    private static readonly HashSet<string> SpecFolders = new(StringComparer.OrdinalIgnoreCase) { "scripts", "references", "assets" };

    // Also common in skills, but just as common in the project a skill works on, so only checked when the skill has one.
    private static readonly HashSet<string> OwnFolders = new(StringComparer.OrdinalIgnoreCase)
    {
        "reference", "examples", "templates", "resources", "docs", "checklists", "data",
    };

    public static IReadOnlyList<SkillFinding> Lint(string body, string folder)
    {
        var root = Path.GetFullPath(folder);
        var findings = new List<SkillFinding>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var target in References(body, root))
        {
            if (seen.Add(target) && Resolve(root, target) is { } full && !File.Exists(full) && !Directory.Exists(full))
            {
                findings.Add(new SkillFinding(FindingKind.MissingFile, target, $"Links to {target}, which is not in the skill folder"));
            }
        }

        return findings;
    }

    private static IEnumerable<string> References(string body, string root)
    {
        Fence? fence = null;
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.TrimStart();
            if (fence is not null)
            {
                fence = fence.ClosedBy(trimmed) ? null : fence;
                continue;
            }

            if (Fence.Opens(trimmed) is { } opened)
            {
                fence = opened;
                continue;
            }

            foreach (var target in InLine(line, root))
            {
                yield return target;
            }
        }
    }

    private static IEnumerable<string> InLine(string line, string root)
    {
        if (ReferenceDefinition().Match(line) is { Success: true } definition && LinkTarget(definition.Groups[1].Value) is { } defined)
        {
            yield return defined;
        }

        foreach (Match code in InlineCode().Matches(line))
        {
            if (Plain(code.Groups[1].Value) is { } target && LooksLikeResource(target, root))
            {
                yield return target;
            }
        }

        foreach (Match link in MarkdownLink().Matches(InlineCode().Replace(line, string.Empty)))
        {
            if (LinkTarget(link.Groups[1].Value) is { } target)
            {
                yield return target;
            }
        }
    }

    private static string? LinkTarget(string raw)
    {
        var target = raw.Trim().Trim('<', '>');
        var cut = target.IndexOfAny(['#', '?']);
        if (cut >= 0)
        {
            target = target[..cut];
        }

        return Plain(target) is { } plain ? Decoded(plain) : null;
    }

    private static string? Decoded(string target)
    {
        var decoded = Uri.UnescapeDataString(target).Replace('\\', '/');
        return decoded.Contains('\0') || decoded.IndexOfAny(Path.GetInvalidPathChars()) >= 0 ? null : decoded;
    }

    private static string? Plain(string raw)
    {
        var target = raw.Replace('\\', '/');
        if (target.Length == 0
            || UriScheme().IsMatch(target)
            || target.StartsWith('/') || target.StartsWith('~')
            || target.IndexOfAny(['$', '{', '}', '*', '<', '>', '|', ' ', '[', ']', '\0']) >= 0
            || target.IndexOfAny(Path.GetInvalidPathChars()) >= 0)
        {
            return null;
        }

        return target.StartsWith("./", StringComparison.Ordinal) ? target[2..] : target;
    }

    private static bool LooksLikeResource(string target, string root)
    {
        var slash = target.IndexOf('/');
        if (slash <= 0 || !Path.HasExtension(target))
        {
            return false;
        }

        var top = target[..slash];
        return SpecFolders.Contains(top) || (OwnFolders.Contains(top) && Directory.Exists(Path.Combine(root, top)));
    }

    private static string? Resolve(string root, string target)
    {
        try
        {
            var full = Path.GetFullPath(Path.Combine(root, target.Replace('/', Path.DirectorySeparatorChar)));
            return full.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ? full : null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    private sealed record Fence(char Marker, int Length)
    {
        public static Fence? Opens(string line)
        {
            if (line.Length < 3 || line[0] is not ('`' or '~'))
            {
                return null;
            }

            var length = line.TakeWhile(c => c == line[0]).Count();
            var rest = line[length..];
            return length >= 3 && !(line[0] == '`' && rest.Contains('`')) ? new Fence(line[0], length) : null;
        }

        public bool ClosedBy(string line) =>
            line.TakeWhile(c => c == Marker).Count() is var run && run >= Length && line[run..].Trim().Length == 0;
    }

    [GeneratedRegex(@"!?\[[^\[\]\n]{0,500}\]\(((?:[^()\s]|\([^()\s]*\)){1,500})(?:\s+(?:""[^""]*""|'[^']*'))?\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"^\s{0,3}\[[^\[\]]{1,200}\]:\s*(\S+)")]
    private static partial Regex ReferenceDefinition();

    [GeneratedRegex(@"`([^`\s]+)`")]
    private static partial Regex InlineCode();

    [GeneratedRegex(@"^[A-Za-z][A-Za-z0-9+.-]*:")]
    private static partial Regex UriScheme();
}

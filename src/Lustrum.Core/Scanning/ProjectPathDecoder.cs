using System.Text.RegularExpressions;

namespace Lustrum.Core.Scanning;

public sealed record DecodedProject(string Path, bool Exists);

/// <summary>
/// Reverses Claude Code's project-folder naming, where every non-alphanumeric
/// character of the original path became '-'. The mapping is lossy, so decoding
/// walks the real file system and keeps only branches whose names re-encode to the input.
/// </summary>
public static partial class ProjectPathDecoder
{
    public static string Encode(string path) =>
        string.Create(path.Length, path, static (span, source) =>
        {
            for (var i = 0; i < source.Length; i++)
            {
                span[i] = char.IsAsciiLetterOrDigit(source[i]) ? source[i] : '-';
            }
        });

    public static DecodedProject Decode(string encodedName)
    {
        if (!TrySplitRoot(encodedName, out var root, out var rest))
        {
            return new DecodedProject(encodedName, false);
        }

        var found = Walk(root, rest);
        return found is null ? new DecodedProject(encodedName, false) : new DecodedProject(found, true);
    }

    private static bool TrySplitRoot(string encodedName, out string root, out string rest)
    {
        var drive = WindowsDrive().Match(encodedName);
        if (drive.Success)
        {
            root = drive.Groups[1].Value + @":\";
            rest = encodedName[drive.Length..];
            return true;
        }

        if (encodedName.StartsWith('-') && encodedName.Length > 1)
        {
            root = Path.DirectorySeparatorChar.ToString();
            rest = encodedName[1..];
            return true;
        }

        root = rest = string.Empty;
        return false;
    }

    private static string? Walk(string directory, string rest)
    {
        if (rest.Length == 0)
        {
            return directory;
        }

        foreach (var (name, encoded) in MatchingChildren(directory, rest))
        {
            var remaining = rest.Length == encoded.Length ? string.Empty : rest[(encoded.Length + 1)..];
            var found = Walk(Path.Combine(directory, name), remaining);
            if (found is not null)
            {
                return found;
            }
        }

        return null;
    }

    private static IEnumerable<(string Name, string Encoded)> MatchingChildren(string directory, string rest)
    {
        IEnumerable<string> children;
        try
        {
            children = Directory.EnumerateDirectories(directory).Select(Path.GetFileName).OfType<string>().ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        return children
            .Select(name => (Name: name, Encoded: Encode(name)))
            .Where(c => rest == c.Encoded || rest.StartsWith(c.Encoded + "-", StringComparison.Ordinal))
            .OrderByDescending(c => c.Encoded.Length)
            .ThenBy(c => c.Name, StringComparer.Ordinal);
    }

    [GeneratedRegex(@"^([A-Za-z])--")]
    private static partial Regex WindowsDrive();
}

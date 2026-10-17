using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Lustrum.Core.Models;

namespace Lustrum.Core.Scanning;

/// <summary>
/// Finds what the Claude desktop app syncs from the claude.ai account: skills under
/// ~/.claude/skills/synced/&lt;account&gt;/ (invoked as anthropic-skills:&lt;name&gt;) and plugins under
/// ~/.claude/plugins/synced/&lt;account&gt;/. The layout is not documented. Plugin folders can exist in several
/// generations (name, name~g2, …); the highest one wins. Dot folders such as .staging are skipped.
/// </summary>
internal static partial class SyncedDiscovery
{
    private const string Origin = "claude.ai";

    public static IEnumerable<SkillSource> Discover(ClaudePaths paths, Action<ScanIssue> report)
    {
        var sources = new List<SkillSource>();
        foreach (var account in Visible(paths.SyncedSkillsDir, report))
        {
            sources.Add(Source(
                $"synced-skills:{Path.GetFileName(account)}",
                SkillSource.AccountSkillsNamespace,
                account,
                SourceLayout.Flat));
        }

        foreach (var account in Visible(paths.SyncedPluginsDir, report))
        {
            foreach (var (folder, logicalName) in NewestGenerations(Visible(account, report)))
            {
                var name = ManifestName(folder, report) ?? logicalName;
                sources.Add(Source($"synced-plugin:{Path.GetFileName(account)}/{logicalName}", name, folder, SourceLayout.Standard));
            }
        }

        return sources;
    }

    public static IEnumerable<string> Visible(string folder, Action<ScanIssue> report) =>
        SafeFileSystem.Directories(folder, report).Where(d => !Path.GetFileName(d).StartsWith('.'));

    private static IEnumerable<(string Folder, string Name)> NewestGenerations(IEnumerable<string> folders) =>
        folders
            .Select(folder => (Folder: folder, Generation: Generation(Path.GetFileName(folder))))
            .GroupBy(f => f.Generation.Name, StringComparer.Ordinal)
            .Select(g => g
                .OrderByDescending(f => f.Generation.Number)
                .ThenBy(f => f.Folder, StringComparer.Ordinal)
                .First())
            .Select(f => (f.Folder, f.Generation.Name));

    // Synced plugins are not listed in enabledPlugins, so there is no switch to honour; they count as enabled.
    private static SkillSource Source(string id, string name, string root, SourceLayout layout) =>
        new(id, SourceKind.Synced, $"{name} ({Origin})", root, Directory.GetLastWriteTimeUtc(root), true, true, name, layout);

    private static (string Name, int Number) Generation(string folderName)
    {
        var match = GenerationSuffix().Match(folderName);
        return match.Success && int.TryParse(match.Groups[2].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            ? (match.Groups[1].Value, number)
            : (folderName, 1);
    }

    private static string? ManifestName(string folder, Action<ScanIssue> report)
    {
        var manifest = Path.Combine(folder, ".claude-plugin", "plugin.json");
        if (!File.Exists(manifest))
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(SharedFile.ReadAllText(manifest));
            var name = doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("name", out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()?.Trim()
                    : null;
            return name is { Length: > 0 } && name.IndexOfAny([':', '/', '\\']) < 0 ? name : null;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            report(new ScanIssue(manifest, $"Cannot read plugin manifest: {ex.Message}"));
            return null;
        }
    }

    [GeneratedRegex("^(.+)~g([0-9]+)$")]
    private static partial Regex GenerationSuffix();
}

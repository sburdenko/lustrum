using System.Text.Json;
using Lustrum.Core.Models;

namespace Lustrum.Core.Scanning;

internal static class ProjectDiscovery
{
    private const string ClaudeFolder = ".claude";

    public static IEnumerable<SkillSource> Discover(ClaudePaths paths, ScanOptions options, Action<ScanIssue> report)
    {
        var known = KnownProjectPaths(paths, report);
        var byEncoded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in known)
        {
            byEncoded.TryAdd(ProjectPathDecoder.Encode(path), path);
        }

        var projects = new Dictionary<string, (string Path, DateTimeOffset? LastSeen)>(PathKey.Comparer);
        foreach (var path in known.Concat(RootProjects(options, report)))
        {
            projects.TryAdd(PathKey.Normalize(path), (path, null));
        }

        foreach (var folder in SafeFileSystem.Directories(paths.ProjectsDir, report))
        {
            var encoded = Path.GetFileName(folder);
            var path = byEncoded.TryGetValue(encoded, out var exact) ? exact : ProjectPathDecoder.Decode(encoded).Path;
            projects[PathKey.Normalize(path)] = (path, Directory.GetLastWriteTimeUtc(folder));
        }

        var home = PathKey.Normalize(paths.Home);
        return projects
            .Where(p => !PathKey.Comparer.Equals(p.Key, home))
            .Select(p => Source(p.Value.Path, p.Value.LastSeen))
            .ToList();
    }

    private static IEnumerable<string> RootProjects(ScanOptions options, Action<ScanIssue> report)
    {
        var excluded = new HashSet<string>(options.ExcludedFolders, StringComparer.OrdinalIgnoreCase);
        var found = new List<string>();
        foreach (var root in options.ExtraRoots)
        {
            if (!Directory.Exists(root))
            {
                report(new ScanIssue(root, "Project search root does not exist"));
                continue;
            }

            Search(root, options.SearchDepth, excluded, found, report);
        }

        return found;
    }

    private static void Search(string directory, int remainingDepth, HashSet<string> excluded, List<string> found, Action<ScanIssue> report)
    {
        if (Directory.Exists(Path.Combine(directory, ClaudeFolder)))
        {
            found.Add(directory);
        }

        if (remainingDepth == 0)
        {
            return;
        }

        foreach (var child in SafeFileSystem.RealDirectories(directory, report))
        {
            var name = Path.GetFileName(child);
            if (name != ClaudeFolder && !excluded.Contains(name))
            {
                Search(child, remainingDepth - 1, excluded, found, report);
            }
        }
    }

    private static SkillSource Source(string projectPath, DateTimeOffset? lastSeen)
    {
        var exists = Path.IsPathRooted(projectPath) && Directory.Exists(projectPath);
        var display = Path.GetFileName(PathKey.Normalize(projectPath));
        return new SkillSource(
            "project:" + projectPath,
            SourceKind.Project,
            display.TrimStart('-'),
            exists ? Path.Combine(projectPath, ClaudeFolder) : projectPath,
            lastSeen,
            exists);
    }

    private static IReadOnlyList<string> KnownProjectPaths(ClaudePaths paths, Action<ScanIssue> report)
    {
        if (!File.Exists(paths.ClaudeJson))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(paths.ClaudeJson));
            return doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("projects", out var projects)
                && projects.ValueKind == JsonValueKind.Object
                ? projects.EnumerateObject().Select(p => p.Name).ToList()
                : [];
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            report(new ScanIssue(paths.ClaudeJson, $"Cannot read project list: {ex.Message}"));
            return [];
        }
    }
}

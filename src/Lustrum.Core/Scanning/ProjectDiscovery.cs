using System.Text.Json;
using Lustrum.Core.Git;
using Lustrum.Core.Models;

namespace Lustrum.Core.Scanning;

internal static class ProjectDiscovery
{
    private const string ClaudeFolder = ".claude";

    public static IReadOnlyList<SkillSource> Discover(ClaudePaths paths, ScanOptions options, Action<ScanIssue> report)
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

    /// <summary>
    /// Folders Claude Code also loads skills from for a known project: every parent up to the repository root, and
    /// every folder below it (to the search depth) that has skills of its own and loads them on demand.
    /// </summary>
    public static IReadOnlyList<SkillSource> Related(IEnumerable<SkillSource> projects, ClaudePaths paths, ScanOptions options, Action<ScanIssue> report)
    {
        var home = PathKey.Normalize(paths.Home);
        var excluded = new HashSet<string>(options.ExcludedFolders, StringComparer.OrdinalIgnoreCase);
        var walked = new HashSet<string>(PathKey.Comparer);
        var realPaths = new Dictionary<string, string>(PathKey.Comparer);
        string Real(string path) => realPaths.TryGetValue(path, out var real) ? real : realPaths[path] = RealPath.Resolve(path);
        var related = new List<SkillSource>();
        foreach (var project in projects.Where(p => p.Exists))
        {
            var folder = ProjectFolder(project);
            related.AddRange(Ancestors(folder, Real)
                .Where(a => !PathKey.Comparer.Equals(PathKey.Normalize(a), home) && HasSkills(a))
                .Select(a => Source(a, null)));
            var nested = new List<string>();
            SearchNested(folder, options.SearchDepth, excluded, walked, nested, report);
            related.AddRange(nested.Select(n => Source(n, null) with { DisplayName = $"{project.DisplayName}/{RelativeName(folder, n)}" }));
        }

        return related;
    }

    public static string ProjectFolder(SkillSource project) =>
        project.Id.StartsWith("project:", StringComparison.Ordinal) ? project.Id["project:".Length..] : project.RootPath;

    private static IEnumerable<string> Ancestors(string folder, Func<string, string> real)
    {
        if (GitRepositoryLocator.FindRoot(folder) is not { } repository)
        {
            yield break;
        }

        for (var current = Path.GetDirectoryName(PathKey.Normalize(folder)); current is not null; current = Path.GetDirectoryName(current))
        {
            if (!PathKey.IsWithin(real(current), repository))
            {
                yield break;
            }

            yield return current;
        }
    }

    // A folder walked for one project is not walked again for a project that contains it.
    private static void SearchNested(string directory, int remainingDepth, HashSet<string> excluded, HashSet<string> walked, List<string> found, Action<ScanIssue> report)
    {
        if (remainingDepth == 0 || !walked.Add(directory))
        {
            return;
        }

        foreach (var child in SafeFileSystem.RealDirectories(directory, report))
        {
            var name = Path.GetFileName(child);
            if (name.StartsWith('.') || excluded.Contains(name))
            {
                continue;
            }

            if (HasSkills(child))
            {
                found.Add(child);
            }

            SearchNested(child, remainingDepth - 1, excluded, walked, found, report);
        }
    }

    private static bool HasSkills(string folder) => Directory.Exists(Path.Combine(folder, ClaudeFolder, "skills"));

    private static string RelativeName(string folder, string nested) =>
        Path.GetRelativePath(folder, nested).Replace(Path.DirectorySeparatorChar, '/');

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
            using var doc = JsonDocument.Parse(SharedFile.ReadAllText(paths.ClaudeJson));
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

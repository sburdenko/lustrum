using System.Text.Json;
using Lustrum.Core.Models;

namespace Lustrum.Core.Scanning;

internal sealed record DiscoveredSources(IReadOnlyList<SkillSource> Sources, IReadOnlyList<ScanIssue> Issues);

internal static class SourceDiscovery
{
    private const string OrphanMarker = ".orphaned_at";

    public static DiscoveredSources Discover(ClaudePaths paths)
    {
        var issues = new List<ScanIssue>();
        var sources = new List<SkillSource> { GlobalSource(paths) };
        sources.AddRange(PluginSources(paths, issues));
        sources.AddRange(ProjectSources(paths, issues));
        return new DiscoveredSources(sources, issues);
    }

    private static SkillSource GlobalSource(ClaudePaths paths) =>
        new("global", SourceKind.Global, "Global", paths.ClaudeDir, null, Directory.Exists(paths.ClaudeDir));

    private static IEnumerable<SkillSource> PluginSources(ClaudePaths paths, List<ScanIssue> issues) =>
        File.Exists(paths.InstalledPluginsJson)
            ? InstalledPlugins(paths.InstalledPluginsJson, issues)
            : CachedPlugins(paths.PluginCacheDir);

    private static IEnumerable<SkillSource> InstalledPlugins(string registryPath, List<ScanIssue> issues)
    {
        var found = new List<SkillSource>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(registryPath));
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("plugins", out var plugins)
                || plugins.ValueKind != JsonValueKind.Object)
            {
                issues.Add(new ScanIssue(registryPath, "Installed plugins file has an unexpected shape"));
                return found;
            }

            foreach (var plugin in plugins.EnumerateObject())
            {
                var source = PluginFromRegistry(plugin.Name, plugin.Value);
                if (source is not null)
                {
                    found.Add(source);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            issues.Add(new ScanIssue(registryPath, $"Cannot read installed plugins: {ex.Message}"));
        }

        return found;
    }

    private static SkillSource? PluginFromRegistry(string key, JsonElement installs)
    {
        var at = key.LastIndexOf('@');
        var plugin = at > 0 ? key[..at] : key;
        var marketplace = at > 0 ? key[(at + 1)..] : "local";
        var installPath = installs.ValueKind == JsonValueKind.Array
            ? installs.EnumerateArray()
                .Select(InstallPathOf)
                .OfType<string>()
                .Where(Path.IsPathRooted)
                .OrderByDescending(Directory.Exists)
                .FirstOrDefault()
            : null;

        return installPath is null ? null : PluginSource(marketplace, plugin, installPath);
    }

    private static string? InstallPathOf(JsonElement install) =>
        install.ValueKind == JsonValueKind.Object
        && install.TryGetProperty("installPath", out var path)
        && path.ValueKind == JsonValueKind.String
            ? path.GetString()
            : null;

    private static IEnumerable<SkillSource> CachedPlugins(string cacheDir)
    {
        if (!Directory.Exists(cacheDir))
        {
            yield break;
        }

        foreach (var marketplaceDir in SafeDirectories(cacheDir))
        {
            foreach (var pluginDir in SafeDirectories(marketplaceDir))
            {
                var live = SafeDirectories(pluginDir)
                    .Where(version => !File.Exists(Path.Combine(version, OrphanMarker)))
                    .OrderByDescending(Directory.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                if (live is not null)
                {
                    yield return PluginSource(Path.GetFileName(marketplaceDir), Path.GetFileName(pluginDir), live);
                }
            }
        }
    }

    private static SkillSource PluginSource(string marketplace, string plugin, string installPath) =>
        new(
            $"plugin:{marketplace}/{plugin}",
            SourceKind.Plugin,
            $"{plugin} ({marketplace})",
            installPath,
            Directory.Exists(installPath) ? Directory.GetLastWriteTimeUtc(installPath) : null,
            Directory.Exists(installPath));

    private static IEnumerable<SkillSource> ProjectSources(ClaudePaths paths, List<ScanIssue> issues)
    {
        var known = KnownProjectPaths(paths, issues);
        var byEncoded = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var path in known)
        {
            byEncoded.TryAdd(ProjectPathDecoder.Encode(path), path);
        }

        var projects = new Dictionary<string, (string Path, DateTimeOffset? LastSeen)>(PathKey.Comparer);

        foreach (var path in known)
        {
            projects[PathKey.Normalize(path)] = (path, null);
        }

        foreach (var folder in SafeDirectories(paths.ProjectsDir))
        {
            var encoded = Path.GetFileName(folder);
            var path = byEncoded.TryGetValue(encoded, out var exact) ? exact : ProjectPathDecoder.Decode(encoded).Path;
            projects[PathKey.Normalize(path)] = (path, Directory.GetLastWriteTimeUtc(folder));
        }

        var home = PathKey.Normalize(paths.Home);
        return projects
            .Where(p => !PathKey.Comparer.Equals(p.Key, home))
            .Select(p => ProjectSource(p.Value.Path, p.Value.LastSeen));
    }

    private static SkillSource ProjectSource(string projectPath, DateTimeOffset? lastSeen)
    {
        var exists = Path.IsPathRooted(projectPath) && Directory.Exists(projectPath);
        var display = Path.GetFileName(PathKey.Normalize(projectPath));
        return new SkillSource(
            "project:" + projectPath,
            SourceKind.Project,
            display.TrimStart('-'),
            exists ? Path.Combine(projectPath, ".claude") : projectPath,
            lastSeen,
            exists);
    }

    private static IReadOnlyList<string> KnownProjectPaths(ClaudePaths paths, List<ScanIssue> issues)
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
            issues.Add(new ScanIssue(paths.ClaudeJson, $"Cannot read project list: {ex.Message}"));
            return [];
        }
    }

    private static IEnumerable<string> SafeDirectories(string path)
    {
        try
        {
            return Directory.Exists(path) ? Directory.EnumerateDirectories(path).ToList() : [];
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

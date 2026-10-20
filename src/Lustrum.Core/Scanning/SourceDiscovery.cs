using Lustrum.Core.Models;

namespace Lustrum.Core.Scanning;

internal sealed record DiscoveredSources(IReadOnlyList<SkillSource> Sources, IReadOnlyList<ScanIssue> Issues);

internal static class SourceDiscovery
{
    public static DiscoveredSources Discover(ClaudePaths paths, ScanOptions options)
    {
        var issues = new List<ScanIssue>();
        var sources = new List<SkillSource>();
        if (Directory.Exists(paths.ManagedClaudeDir))
        {
            sources.Add(new SkillSource("managed", SourceKind.Managed, "Managed", paths.ManagedClaudeDir, null, true));
        }

        sources.Add(GlobalSource(paths));
        sources.AddRange(PluginDiscovery.Discover(paths, issues.Add));
        var projects = ProjectDiscovery.Discover(paths, options, issues.Add);
        sources.AddRange(projects);
        sources.AddRange(ProjectDiscovery.Related(projects, paths, options, issues.Add));
        sources.AddRange(SyncedDiscovery.Discover(paths, issues.Add));
        var unique = sources.DistinctBy(Key, PathKey.Comparer).ToList();
        return new DiscoveredSources(unique, issues);
    }

    // The same folder can arrive written differently: with a trailing separator, other casing or the other slash.
    private static string Key(SkillSource source) =>
        source.ProjectFolder is { } folder ? "project:" + PathKey.Normalize(folder) : source.Id;

    private static SkillSource GlobalSource(ClaudePaths paths) =>
        new("global", SourceKind.Global, "Global", paths.ClaudeDir, null, Directory.Exists(paths.ClaudeDir));
}

using Lustrum.Core.Models;

namespace Lustrum.Core.Scanning;

internal sealed record DiscoveredSources(IReadOnlyList<SkillSource> Sources, IReadOnlyList<ScanIssue> Issues);

internal static class SourceDiscovery
{
    public static DiscoveredSources Discover(ClaudePaths paths, ScanOptions options)
    {
        var issues = new List<ScanIssue>();
        var sources = new List<SkillSource> { GlobalSource(paths) };
        sources.AddRange(PluginDiscovery.Discover(paths, issues.Add));
        sources.AddRange(ProjectDiscovery.Discover(paths, options, issues.Add));
        sources.AddRange(SyncedDiscovery.Discover(paths, issues.Add));
        var unique = sources.DistinctBy(s => s.Id, StringComparer.Ordinal).ToList();
        return new DiscoveredSources(unique, issues);
    }

    private static SkillSource GlobalSource(ClaudePaths paths) =>
        new("global", SourceKind.Global, "Global", paths.ClaudeDir, null, Directory.Exists(paths.ClaudeDir));
}

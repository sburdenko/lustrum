using FluentIcons.Common;
using Lustrum.Core.Indexing;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public static class NavTreeBuilder
{
    public static IReadOnlyList<NavNode> Build(ScanResult scan, bool includeEmptySources = false)
    {
        var countBySource = scan.Skills.GroupBy(s => s.Source.Id).ToDictionary(g => g.Key, g => g.Count());
        int CountOf(SkillSource source) => countBySource.GetValueOrDefault(source.Id);
        int CountKind(SourceKind kind) => scan.Skills.Count(s => s.Source.Kind == kind);

        return
        [
            new NavNode("All", SkillScope.All, scan.Skills.Count, Symbol.Apps, []),
            new NavNode("Global", new KindScope(SourceKind.Global), CountKind(SourceKind.Global), Symbol.Globe, []),
            new NavNode("Projects", new KindScope(SourceKind.Project), CountKind(SourceKind.Project), Symbol.FolderOpen, ProjectNodes(scan, CountOf, includeEmptySources)),
            new NavNode("Plugins", new KindScope(SourceKind.Plugin), CountKind(SourceKind.Plugin), Symbol.PuzzlePiece, PluginNodes(scan, CountOf)),
            new NavNode("Problems", SkillScope.Problems, scan.Skills.Count(SkillScope.Problems.Includes), Symbol.Warning, []),
        ];
    }

    private static IReadOnlyList<NavNode> ProjectNodes(ScanResult scan, Func<SkillSource, int> countOf, bool includeEmpty) =>
        scan.Sources
            .Where(s => s.Kind == SourceKind.Project)
            .Select(source => (Source: source, Count: countOf(source)))
            .Where(p => includeEmpty || p.Count > 0)
            .OrderByDescending(p => p.Count > 0)
            .ThenBy(p => p.Source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(p => new NavNode(
                p.Source.DisplayName,
                new SourceScope(p.Source.Id),
                p.Count,
                p.Source.Exists ? Symbol.Folder : Symbol.FolderProhibited,
                [],
                IsDimmed: !p.Source.Exists || p.Count == 0,
                Hint: p.Source.Exists ? p.Source.RootPath : $"Project folder not found: {p.Source.RootPath}"))
            .ToList();

    private static IReadOnlyList<NavNode> PluginNodes(ScanResult scan, Func<SkillSource, int> countOf) =>
        scan.Sources
            .Where(s => s.Kind == SourceKind.Plugin)
            .GroupBy(Marketplace)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => new NavNode(
                group.Key,
                new MarketplaceScope(group.Key),
                group.Sum(countOf),
                Symbol.Building,
                group
                    .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .Select(s => new NavNode(
                        PluginTitle(s),
                        new SourceScope(s.Id),
                        countOf(s),
                        Symbol.PuzzlePiece,
                        [],
                        IsDimmed: countOf(s) == 0,
                        Hint: s.RootPath))
                    .ToList()))
            .ToList();

    private static string Marketplace(SkillSource source)
    {
        var tail = source.Id["plugin:".Length..];
        return tail[..tail.IndexOf('/')];
    }

    private static string PluginTitle(SkillSource source)
    {
        var tail = source.Id["plugin:".Length..];
        return tail[(tail.IndexOf('/') + 1)..];
    }
}

using Lustrum.Core.Indexing;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.Effective;

/// <summary>
/// Resolves what Claude Code loads in one project: that project's skills, global skills and enabled
/// plugins. A project skill wins over a global skill with the same name; plugin skills are always prefixed.
/// </summary>
public static class EffectiveSetBuilder
{
    private const string ProjectPrefix = "project:";

    public static EffectiveSet Build(ScanResult scan, SkillSource project)
    {
        var candidates = scan.Skills.Where(new EffectiveScope(project.Id).Includes).ToList();
        var resolved = candidates
            .Where(s => s.Source.Kind != SourceKind.Plugin)
            .GroupBy(s => s.Name, StringComparer.Ordinal)
            .SelectMany(Resolve)
            .DistinctBy(e => e.Skill.Id, StringComparer.Ordinal)
            .ToDictionary(e => e.Skill.Id, StringComparer.Ordinal);

        var entries = candidates
            .Select(s => resolved.GetValueOrDefault(s.Id) ?? Active(s, []))
            .ToList();
        return new EffectiveSet(project, entries);
    }

    public static SkillSource? FindProject(ScanResult scan, string projectPath)
    {
        var wanted = PathKey.Normalize(Path.GetFullPath(projectPath));
        return scan.Sources.FirstOrDefault(s =>
            s.Kind == SourceKind.Project
            && PathKey.Comparer.Equals(PathKey.Normalize(s.Id[ProjectPrefix.Length..]), wanted));
    }

    private static IEnumerable<EffectiveEntry> Resolve(IGrouping<string, Skill> sameName)
    {
        var best = sameName.Min(Rank);
        var winners = sameName.Where(s => Rank(s) == best).ToList();
        var losers = sameName.Where(s => Rank(s) != best).ToList();
        return winners
            .Select(w => Active(w, losers))
            .Concat(losers.Select(l => new EffectiveEntry(l, Availability.Shadowed, winners[0], [])));
    }

    private static EffectiveEntry Active(Skill skill, IReadOnlyList<Skill> overrides) =>
        new(skill, Availability.Active, null, overrides);

    private static int Rank(Skill skill) => skill.Source.Kind == SourceKind.Project ? 0 : 1;
}

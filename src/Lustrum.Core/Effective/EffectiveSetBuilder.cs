using Lustrum.Core.Indexing;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.Effective;

/// <summary>
/// Resolves what Claude Code loads in one project: that project's skills, global skills, enabled plugins and
/// skills synced from the claude.ai account. Within one name a skill beats a command, and a personal (global)
/// copy beats a project copy, as Claude Code resolves them; plugin and synced skills are always prefixed.
/// </summary>
public static class EffectiveSetBuilder
{
    private const string ProjectPrefix = "project:";

    public static EffectiveSet Build(ScanResult scan, SkillSource project)
    {
        var candidates = scan.Skills.Where(new EffectiveScope(project.Id).Includes).ToList();
        var resolved = candidates
            .Where(s => !s.Source.IsNamespaced)
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

    private static int Rank(Skill skill) =>
        (skill.Kind == SkillKind.Command ? 2 : 0) + (skill.Source.Kind == SourceKind.Project ? 1 : 0);
}

using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.Effective;

/// <summary>
/// Resolves what Claude Code loads in one project, as described by its <see cref="ProjectContext"/>. Within one plain
/// name a skill beats a command, and managed beats personal beats project, as Claude Code resolves them. Copies in
/// the project and its parent folders all load; skills in folders below the project load on demand and never compete
/// for a name, because Claude Code qualifies them with their folder. Plugin and synced skills are always prefixed.
/// </summary>
public static class EffectiveSetBuilder
{
    public static EffectiveSet Build(ScanResult scan, SkillSource project) => Build(scan, ProjectContext.For(scan, project));

    public static EffectiveSet Build(ScanResult scan, ProjectContext context)
    {
        var candidates = scan.Skills.Where(context.Includes).ToList();
        var resolved = candidates
            .Where(s => !s.Source.IsNamespaced && context.OnDemandFolder(s.Source) is null)
            .GroupBy(s => s.Name, StringComparer.Ordinal)
            .SelectMany(Resolve)
            .DistinctBy(e => e.Skill.Id, StringComparer.Ordinal)
            .ToDictionary(e => e.Skill.Id, StringComparer.Ordinal);

        var entries = candidates
            .Select(s => context.OnDemandFolder(s.Source) is { } folder
                ? new EffectiveEntry(s, Availability.OnDemand, null, [], LoadsUnder: folder)
                : resolved.GetValueOrDefault(s.Id) ?? Active(s, []))
            .Select(e => WithVisibility(e, scan.Settings, context.Project))
            .ToList();
        return new EffectiveSet(context.Project, entries);
    }

    public static SkillSource? FindProject(ScanResult scan, string projectPath)
    {
        var wanted = PathKey.Normalize(Path.GetFullPath(projectPath));
        return scan.Sources.FirstOrDefault(s =>
            s.ProjectFolder is { } folder && PathKey.Comparer.Equals(PathKey.Normalize(folder), wanted));
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

    private static EffectiveEntry WithVisibility(EffectiveEntry entry, SettingsLayers settings, SkillSource project) =>
        entry.Availability != Availability.Shadowed && SettingsLayers.Applies(entry.Skill)
            ? entry with { Visibility = settings.Resolve(entry.Skill.Name, project) }
            : entry;

    private static EffectiveEntry Active(Skill skill, IReadOnlyList<Skill> overrides) =>
        new(skill, Availability.Active, null, overrides);

    private static int Rank(Skill skill) => (skill.Kind == SkillKind.Command ? 10 : 0) + skill.Source.Kind switch
    {
        SourceKind.Managed => 0,
        SourceKind.Global => 1,
        _ => 2,
    };
}

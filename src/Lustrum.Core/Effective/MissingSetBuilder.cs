using Lustrum.Core.Indexing;
using Lustrum.Core.Models;

namespace Lustrum.Core.Effective;

/// <summary>
/// The opposite of the effective set: every skill on the machine that the project does not load, either because
/// it lives in another project or because its plugin is disabled.
/// </summary>
public static class MissingSetBuilder
{
    public static MissingSet Build(ScanResult scan, SkillSource project)
    {
        var loaded = EffectiveSetBuilder.Build(scan, project).Entries
            .Where(e => e.Availability == Availability.Active)
            .GroupBy(e => e.Skill.ConflictKey, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Skill, StringComparer.Ordinal);

        var entries = scan.Skills
            .Where(new MissingScope(project.Id).Includes)
            .Select(s => new MissingEntry(s, loaded.GetValueOrDefault(s.ConflictKey)))
            .ToList();
        return new MissingSet(project, entries);
    }
}

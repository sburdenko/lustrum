using Lustrum.Core.Models;

namespace Lustrum.Core.Effective;

public sealed record MissingSet(SkillSource Project, IReadOnlyList<MissingEntry> Entries)
{
    private readonly Dictionary<string, MissingEntry> _byId = Entries
        .GroupBy(e => e.Skill.Id, StringComparer.Ordinal)
        .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

    public MissingEntry? Find(string skillId) => _byId.GetValueOrDefault(skillId);
}

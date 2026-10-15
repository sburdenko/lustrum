using Lustrum.Core.Models;

namespace Lustrum.Core.Effective;

public sealed record EffectiveSet(SkillSource Project, IReadOnlyList<EffectiveEntry> Entries)
{
    private readonly Dictionary<string, EffectiveEntry> _byId = Index(Entries);

    public EffectiveEntry? Find(string skillId) => _byId.GetValueOrDefault(skillId);

    private static Dictionary<string, EffectiveEntry> Index(IEnumerable<EffectiveEntry> entries)
    {
        var byId = new Dictionary<string, EffectiveEntry>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            byId.TryAdd(entry.Skill.Id, entry);
        }

        return byId;
    }
}

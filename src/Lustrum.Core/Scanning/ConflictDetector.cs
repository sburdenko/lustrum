using Lustrum.Core.Models;

namespace Lustrum.Core.Scanning;

internal static class ConflictDetector
{
    public static IReadOnlyList<Conflict> Detect(IReadOnlyList<Skill> skills)
    {
        var names = SkillTagger.DuplicatedNames(skills);
        return skills
            .Where(s => names.Contains(s.ConflictKey))
            .GroupBy(s => s.ConflictKey, StringComparer.Ordinal)
            .Select(g => new Conflict(g.Key, g.ToList()))
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}

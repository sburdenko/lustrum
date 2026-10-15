using Lustrum.Core.Models;

namespace Lustrum.Core.Effective;

public sealed record EffectiveEntry(
    Skill Skill,
    Availability Availability,
    Skill? ShadowedBy,
    IReadOnlyList<Skill> Overrides)
{
    public string? Label(Func<SkillSource, string> sourceName)
    {
        if (ShadowedBy is { } winner)
        {
            return $"shadowed by {sourceName(winner.Source)}";
        }

        return Overrides.Count > 0
            ? "overrides " + string.Join(", ", Overrides.Select(o => sourceName(o.Source)).Distinct())
            : null;
    }
}

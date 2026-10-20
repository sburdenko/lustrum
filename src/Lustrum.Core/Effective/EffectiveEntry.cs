using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Models;

namespace Lustrum.Core.Effective;

public sealed record EffectiveEntry(
    Skill Skill,
    Availability Availability,
    Skill? ShadowedBy,
    IReadOnlyList<Skill> Overrides,
    ResolvedVisibility? Visibility = null,
    string? LoadsUnder = null)
{
    public SkillVisibility VisibilityValue => Visibility?.Value ?? SkillVisibility.On;

    /// <summary>Loaded but hidden from both Claude and the / menu by skillOverrides.</summary>
    public bool IsTurnedOff => Availability == Availability.Active && VisibilityValue == SkillVisibility.Off;

    public string? Label(Func<SkillSource, string> sourceName)
    {
        if (ShadowedBy is { } winner)
        {
            return $"shadowed by {sourceName(winner.Source)}";
        }

        if (Availability == Availability.OnDemand)
        {
            return $"on demand · {LoadsUnder}";
        }

        if (VisibilityValue.Label() is { } visibility)
        {
            return visibility;
        }

        return Overrides.Count > 0
            ? "overrides " + string.Join(", ", Overrides.Select(o => sourceName(o.Source)).Distinct())
            : null;
    }
}

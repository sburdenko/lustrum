using Lustrum.Core.Models;

namespace Lustrum.Core.Effective;

public sealed record EffectiveEntry(
    Skill Skill,
    Availability Availability,
    Skill? ShadowedBy,
    IReadOnlyList<Skill> Overrides);

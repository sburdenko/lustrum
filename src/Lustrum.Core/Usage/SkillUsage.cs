namespace Lustrum.Core.Usage;

public sealed record SkillUsage(
    string Name,
    int Count,
    DateTimeOffset LastUsed,
    IReadOnlyList<string> Projects,
    int ByModel,
    int ByUser);

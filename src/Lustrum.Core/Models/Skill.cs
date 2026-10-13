namespace Lustrum.Core.Models;

public sealed record Skill(
    string Id,
    string Name,
    string FullName,
    string? Description,
    SkillKind Kind,
    SkillSource Source,
    string Path,
    string? Body,
    IReadOnlyDictionary<string, string> Frontmatter,
    IReadOnlyList<string> AllowedTools,
    IReadOnlyList<string> Files,
    long SizeBytes,
    DateTimeOffset Modified,
    Health Health,
    IReadOnlyList<string> Tags)
{
    public bool IsBroken => Health is not (Health.Ok or Health.DescriptionTooLong);

    public string InvocationName => Kind == SkillKind.Command ? "/" + FullName : FullName;
}

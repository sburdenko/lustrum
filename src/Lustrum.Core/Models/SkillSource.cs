namespace Lustrum.Core.Models;

public sealed record SkillSource(
    string Id,
    SourceKind Kind,
    string DisplayName,
    string RootPath,
    DateTimeOffset? LastSeen,
    bool Exists);

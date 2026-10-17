namespace Lustrum.Core.Models;

public sealed record SkillSource(
    string Id,
    SourceKind Kind,
    string DisplayName,
    string RootPath,
    DateTimeOffset? LastSeen,
    bool Exists,
    bool IsEnabled = true,
    string? Namespace = null,
    SourceLayout Layout = SourceLayout.Standard)
{
    public const string AccountSkillsNamespace = "anthropic-skills";

    public bool IsNamespaced => Namespace is not null;
}

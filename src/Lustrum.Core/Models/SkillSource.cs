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

    /// <summary>The project folder of a project source, taken from its id; null for any other source.</summary>
    public string? ProjectFolder =>
        Kind == SourceKind.Project && Id.StartsWith("project:", StringComparison.Ordinal) ? Id["project:".Length..] : null;
}

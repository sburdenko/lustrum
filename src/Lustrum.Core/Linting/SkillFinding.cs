namespace Lustrum.Core.Linting;

/// <summary>Something worth fixing in a skill that its health does not cover. Subject is the path or tool entry.</summary>
public sealed record SkillFinding(FindingKind Kind, string Subject, string Message);

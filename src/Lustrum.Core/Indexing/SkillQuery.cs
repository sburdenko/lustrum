namespace Lustrum.Core.Indexing;

public sealed record SkillQuery(SkillScope Scope, string Text, IReadOnlyList<string>? RequiredTags = null);

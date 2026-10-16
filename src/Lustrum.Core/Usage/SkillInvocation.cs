namespace Lustrum.Core.Usage;

public sealed record SkillInvocation(string Name, DateTimeOffset At, string? ProjectPath, InvocationKind Kind);

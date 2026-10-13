namespace Lustrum.Core.Models;

public sealed record Conflict(string Name, IReadOnlyList<Skill> Skills);

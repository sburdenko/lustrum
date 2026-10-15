using Lustrum.Core.Models;

namespace Lustrum.Core.Problems;

public sealed record Problem(ProblemKind Kind, string Subject, string Path, string? Detail, Skill? Skill);

namespace Lustrum.Core.Git;

/// <summary>
/// What the fetched upstream changes in one skill: how many incoming commits touch it, which of its files they change
/// since the merge base, and its main file at the merge base and upstream. Before or After is null when the file does
/// not exist on that side.
/// </summary>
public sealed record SkillIncoming(int Commits, IReadOnlyList<GitChange> Files, string MainFile, string? Before, string? After);

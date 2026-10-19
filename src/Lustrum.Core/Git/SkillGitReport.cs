namespace Lustrum.Core.Git;

/// <summary>
/// Git facts for one skill. Changes are those under the skill folder; Update is judged on the whole repository.
/// </summary>
public sealed record SkillGitReport(
    SkillLocation Location,
    GitTracking Tracking,
    IReadOnlyList<GitChange> Changes,
    GitCommit? LastCommit,
    UpdateDecision Update)
{
    public SkillRepository Repository => Location.Repository;
}

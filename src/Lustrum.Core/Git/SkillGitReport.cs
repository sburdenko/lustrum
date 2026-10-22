namespace Lustrum.Core.Git;

/// <summary>
/// Git facts for one skill. Changes and Incoming are those under the skill folder; Update is judged on the whole
/// repository. Incoming is null when the branch is not behind its upstream.
/// </summary>
public sealed record SkillGitReport(
    SkillLocation Location,
    GitTracking Tracking,
    IReadOnlyList<GitChange> Changes,
    GitCommit? LastCommit,
    UpdateDecision Update,
    SkillIncoming? Incoming = null)
{
    public SkillRepository Repository => Location.Repository;
}

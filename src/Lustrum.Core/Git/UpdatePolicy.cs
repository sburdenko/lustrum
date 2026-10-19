namespace Lustrum.Core.Git;

/// <summary>
/// Decides whether a repository may be fast-forwarded from Lustrum. Only repositories that hold nothing but
/// skills qualify, and only when the update cannot lose or merge anything: a clean tree and no local commits.
/// </summary>
public static class UpdatePolicy
{
    public static UpdateDecision Evaluate(SkillRepository repository, GitTracking tracking, IReadOnlyList<GitChange> repositoryChanges)
    {
        if (repository.Role == RepositoryRole.Containing)
        {
            return NotSkillsOnly(repository);
        }

        if (tracking.Branch is not { } branch)
        {
            return new UpdateBlocked("HEAD is detached, so there is no branch to update");
        }

        if (tracking.Upstream is not { } upstream)
        {
            return new UpdateBlocked($"Branch {branch} has no upstream to update from");
        }

        if (tracking.Behind == 0)
        {
            return new UpToDate();
        }

        if (repositoryChanges.Count > 0)
        {
            return new UpdateBlocked("The repository has uncommitted changes");
        }

        if (tracking.Ahead > 0)
        {
            var commits = tracking.Ahead == 1 ? "1 local commit" : $"{tracking.Ahead} local commits";
            return new UpdateBlocked($"Branch {branch} has {commits} that {upstream} does not have");
        }

        return new UpdateAvailable(tracking.Behind);
    }

    public static UpdateBlocked NotSkillsOnly(SkillRepository repository) =>
        new($"This skill is part of the {repository.Name} repository; update it there with git");
}

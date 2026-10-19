using Lustrum.Core.Models;

namespace Lustrum.Core.Git;

public sealed class SkillGit(IGitClient git) : ISkillGit
{
    public SkillLocation? Locate(Skill skill) => GitRepositoryLocator.Locate(skill);

    public async Task<SkillGitReport?> InspectAsync(Skill skill, CancellationToken cancellationToken)
    {
        if (await Task.Run(() => Locate(skill), cancellationToken).ConfigureAwait(false) is not { } location)
        {
            return null;
        }

        var root = location.Repository.Root;
        var tracking = await git.TrackingAsync(root, cancellationToken).ConfigureAwait(false);
        var skillsOnly = location.Repository.Role == RepositoryRole.SkillsOnly;
        var changes = await git.ChangesAsync(root, skillsOnly ? null : location.RelativePath, cancellationToken).ConfigureAwait(false);
        var lastCommit = await git.LastCommitAsync(root, location.RelativePath, cancellationToken).ConfigureAwait(false);
        return new SkillGitReport(
            location,
            tracking,
            changes.Where(location.Contains).ToList(),
            lastCommit,
            UpdatePolicy.Evaluate(location.Repository, tracking, changes));
    }

    public async Task<GitTracking> CheckForUpdatesAsync(SkillRepository repository, CancellationToken cancellationToken)
    {
        var tracking = await git.TrackingAsync(repository.Root, cancellationToken).ConfigureAwait(false);
        if (!tracking.HasUpstream)
        {
            return tracking;
        }

        await git.FetchAsync(repository.Root, cancellationToken).ConfigureAwait(false);
        return await git.TrackingAsync(repository.Root, cancellationToken).ConfigureAwait(false);
    }

    public async Task<GitTracking> UpdateAsync(SkillRepository repository, CancellationToken cancellationToken)
    {
        if (repository.Role != RepositoryRole.SkillsOnly)
        {
            throw new GitUpdateRefusedException(UpdatePolicy.NotSkillsOnly(repository).Reason);
        }

        var tracking = await CheckForUpdatesAsync(repository, cancellationToken).ConfigureAwait(false);
        var changes = await git.ChangesAsync(repository.Root, null, cancellationToken).ConfigureAwait(false);
        switch (UpdatePolicy.Evaluate(repository, tracking, changes))
        {
            case UpdateBlocked blocked:
                throw new GitUpdateRefusedException(blocked.Reason);
            case UpToDate:
                return tracking;
        }

        if (await OverwrittenIgnoredPathAsync(repository.Root, cancellationToken).ConfigureAwait(false) is { } path)
        {
            throw new GitUpdateRefusedException($"The update would overwrite {path}, which git ignores here");
        }

        await git.FastForwardAsync(repository.Root, cancellationToken).ConfigureAwait(false);
        return await git.TrackingAsync(repository.Root, cancellationToken).ConfigureAwait(false);
    }

    // A fast-forward silently replaces ignored files that the upstream starts tracking.
    private async Task<string?> OverwrittenIgnoredPathAsync(string root, CancellationToken cancellationToken)
    {
        var ignored = await git.IgnoredPathsAsync(root, cancellationToken).ConfigureAwait(false);
        if (ignored.Count == 0)
        {
            return null;
        }

        var incoming = await git.IncomingNewPathsAsync(root, cancellationToken).ConfigureAwait(false);
        return incoming.FirstOrDefault(path => ignored.Any(entry => entry.EndsWith('/') ? path.StartsWith(entry, StringComparison.Ordinal) : path == entry));
    }
}

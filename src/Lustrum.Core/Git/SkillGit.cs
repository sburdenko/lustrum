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
        var incoming = tracking is { HasUpstream: true, Behind: > 0 }
            ? await IncomingAsync(location, MainFile(skill, location), cancellationToken).ConfigureAwait(false)
            : null;
        return new SkillGitReport(
            location,
            tracking,
            changes.Where(location.Contains).ToList(),
            lastCommit,
            UpdatePolicy.Evaluate(location.Repository, tracking, changes),
            incoming);
    }

    public async Task<IReadOnlyDictionary<string, int>> IncomingCommitsAsync(
        SkillRepository repository, IReadOnlyCollection<string> relativePaths, CancellationToken cancellationToken)
    {
        var commits = await git.IncomingCommitFilesAsync(repository.Root, cancellationToken).ConfigureAwait(false);
        return relativePaths
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(path => path, path => commits.Count(files => files.Any(file => Touches(path, file))), StringComparer.Ordinal);
    }

    private static bool Touches(string relativePath, string file) =>
        relativePath == "." || file == relativePath || file.StartsWith(relativePath + "/", StringComparison.Ordinal);

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

    private async Task<SkillIncoming> IncomingAsync(SkillLocation location, string mainFile, CancellationToken cancellationToken)
    {
        var root = location.Repository.Root;
        var counts = await IncomingCommitsAsync(location.Repository, [location.RelativePath], cancellationToken).ConfigureAwait(false);
        var files = await git.IncomingChangesAsync(root, location.RelativePath, cancellationToken).ConfigureAwait(false);
        var mergeBase = await git.MergeBaseAsync(root, cancellationToken).ConfigureAwait(false);
        var before = await git.ShowAsync(root, mergeBase, mainFile, cancellationToken).ConfigureAwait(false);
        var after = await git.ShowAsync(root, "@{u}", mainFile, cancellationToken).ConfigureAwait(false);
        return new SkillIncoming(counts[location.RelativePath], files, mainFile, before, after);
    }

    private static string MainFile(Skill skill, SkillLocation location) => (skill.Kind, location.RelativePath) switch
    {
        (SkillKind.Command, var file) => file,
        (_, ".") => Path.GetFileName(skill.Path),
        (_, var folder) => $"{folder}/{Path.GetFileName(skill.Path)}",
    };

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

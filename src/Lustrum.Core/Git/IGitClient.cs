namespace Lustrum.Core.Git;

public interface IGitClient
{
    Task<GitTracking> TrackingAsync(string root, CancellationToken cancellationToken);

    /// <summary>Uncommitted changes in the whole repository, or only under <paramref name="relativePath"/>.</summary>
    Task<IReadOnlyList<GitChange>> ChangesAsync(string root, string? relativePath, CancellationToken cancellationToken);

    Task<GitCommit?> LastCommitAsync(string root, string relativePath, CancellationToken cancellationToken);

    /// <summary>Paths that exist on disk but are ignored by git, as git status --ignored lists them.</summary>
    Task<IReadOnlyList<string>> IgnoredPathsAsync(string root, CancellationToken cancellationToken);

    /// <summary>Paths the upstream adds compared with HEAD.</summary>
    Task<IReadOnlyList<string>> IncomingNewPathsAsync(string root, CancellationToken cancellationToken);

    Task FetchAsync(string root, CancellationToken cancellationToken);

    Task FastForwardAsync(string root, CancellationToken cancellationToken);
}

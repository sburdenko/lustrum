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

    /// <summary>For each commit the upstream has that HEAD does not, the files it changes.</summary>
    Task<IReadOnlyList<IReadOnlyList<string>>> IncomingCommitFilesAsync(string root, CancellationToken cancellationToken);

    /// <summary>The commit HEAD and the upstream last had in common.</summary>
    Task<string> MergeBaseAsync(string root, CancellationToken cancellationToken);

    /// <summary>
    /// Files under <paramref name="relativePath"/> that the upstream changes since the merge base, so commits that
    /// exist only locally are not shown as changes coming in.
    /// </summary>
    Task<IReadOnlyList<GitChange>> IncomingChangesAsync(string root, string relativePath, CancellationToken cancellationToken);

    /// <summary>A file's content at a revision, or null when the file does not exist there.</summary>
    Task<string?> ShowAsync(string root, string revision, string relativeFile, CancellationToken cancellationToken);

    Task FetchAsync(string root, CancellationToken cancellationToken);

    Task FastForwardAsync(string root, CancellationToken cancellationToken);
}

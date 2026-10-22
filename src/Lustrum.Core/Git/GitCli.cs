using System.Globalization;

namespace Lustrum.Core.Git;

/// <summary>Runs the git command line; there is no bundled git library.</summary>
public sealed class GitCli(string executable = "git") : IGitClient
{
    private const char Separator = '\u001f';
    private const char CommitSeparator = '\u001e';
    private static readonly TimeSpan LocalTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan NetworkTimeout = TimeSpan.FromSeconds(90);

    private readonly GitProcess _git = new(executable);

    public async Task<GitTracking> TrackingAsync(string root, CancellationToken cancellationToken)
    {
        var head = await RunAsync(root, ["symbolic-ref", "--quiet", "--short", "HEAD"], cancellationToken).ConfigureAwait(false);
        if (!head.Succeeded)
        {
            return head.ExitCode == 1 ? GitTracking.Detached : throw Failed("symbolic-ref", head);
        }

        var branch = head.Stdout.Trim();
        var upstream = await RunAsync(root, ["rev-parse", "--abbrev-ref", "--symbolic-full-name", "@{u}"], cancellationToken).ConfigureAwait(false);
        if (!upstream.Succeeded)
        {
            return new GitTracking(branch, null, 0, 0);
        }

        var counts = await RunAsync(root, ["rev-list", "--left-right", "--count", "HEAD...@{u}"], cancellationToken).ConfigureAwait(false);
        if (!counts.Succeeded)
        {
            throw Failed("rev-list", counts);
        }

        var parts = counts.Stdout.Split((char[])['\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new GitTracking(branch, upstream.Stdout.Trim(), ParseCount(parts, 0), ParseCount(parts, 1));
    }

    public async Task<IReadOnlyList<GitChange>> ChangesAsync(string root, string? relativePath, CancellationToken cancellationToken)
    {
        string[] scope = relativePath is null ? [] : ["--", Literal(relativePath)];
        var status = await RunAsync(root, ["status", "--porcelain=v1", "-z", "--untracked-files=all", .. scope], cancellationToken).ConfigureAwait(false);
        return status.Succeeded ? GitStatusParser.Parse(status.Stdout) : throw Failed("status", status);
    }

    public async Task<GitCommit?> LastCommitAsync(string root, string relativePath, CancellationToken cancellationToken)
    {
        var log = await RunAsync(
            root,
            ["log", "-1", "--format=%H%x1f%h%x1f%cI%x1f%an%x1f%s", "--", Literal(relativePath)],
            cancellationToken).ConfigureAwait(false);
        if (!log.Succeeded)
        {
            return log.Stderr.Contains("does not have any commits", StringComparison.Ordinal) ? null : throw Failed("log", log);
        }

        var fields = log.Stdout.TrimEnd('\n', '\r').Split(Separator);
        return fields.Length == 5 && DateTimeOffset.TryParse(fields[2], CultureInfo.InvariantCulture, out var when)
            ? new GitCommit(fields[0], fields[1], when, fields[3], fields[4])
            : null;
    }

    public async Task<IReadOnlyList<string>> IgnoredPathsAsync(string root, CancellationToken cancellationToken)
    {
        var status = await RunAsync(root, ["status", "--porcelain=v1", "-z", "--ignored", "--untracked-files=all"], cancellationToken).ConfigureAwait(false);
        return status.Succeeded
            ? status.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(e => e.StartsWith("!! ", StringComparison.Ordinal)).Select(e => e[3..]).ToList()
            : throw Failed("status", status);
    }

    public async Task<IReadOnlyList<string>> IncomingNewPathsAsync(string root, CancellationToken cancellationToken)
    {
        var diff = await RunAsync(root, ["diff", "--name-only", "-z", "--no-renames", "--diff-filter=A", "HEAD", "@{u}"], cancellationToken).ConfigureAwait(false);
        return diff.Succeeded ? diff.Stdout.Split('\0', StringSplitOptions.RemoveEmptyEntries) : throw Failed("diff", diff);
    }

    public async Task<IReadOnlyList<IReadOnlyList<string>>> IncomingCommitFilesAsync(string root, CancellationToken cancellationToken)
    {
        var log = await RunAsync(root, ["log", "--format=%x1e", "--name-only", "-z", "--no-renames", "HEAD..@{u}"], cancellationToken).ConfigureAwait(false);
        if (!log.Succeeded)
        {
            throw Failed("log", log);
        }

        return log.Stdout
            .Split(CommitSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(commit => (IReadOnlyList<string>)commit.Split('\0').Select(f => f.Trim('\n')).Where(f => f.Length > 0).ToList())
            .ToList();
    }

    public async Task<string> MergeBaseAsync(string root, CancellationToken cancellationToken)
    {
        var mergeBase = await RunAsync(root, ["merge-base", "HEAD", "@{u}"], cancellationToken).ConfigureAwait(false);
        return mergeBase.Succeeded ? mergeBase.Stdout.Trim() : throw Failed("merge-base", mergeBase);
    }

    public async Task<IReadOnlyList<GitChange>> IncomingChangesAsync(string root, string relativePath, CancellationToken cancellationToken)
    {
        var diff = await RunAsync(root, ["diff", "--name-status", "-z", "--no-renames", "HEAD...@{u}", "--", Literal(relativePath)], cancellationToken).ConfigureAwait(false);
        return diff.Succeeded ? ParseNameStatus(diff.Stdout) : throw Failed("diff", diff);
    }

    public async Task<string?> ShowAsync(string root, string revision, string relativeFile, CancellationToken cancellationToken)
    {
        var show = await RunAsync(root, ["show", $"{revision}:{relativeFile}"], cancellationToken).ConfigureAwait(false);
        if (show.Succeeded)
        {
            return show.Stdout;
        }

        return show.Stderr.Contains("does not exist", StringComparison.Ordinal) || show.Stderr.Contains("exists on disk, but not in", StringComparison.Ordinal)
            ? null
            : throw Failed("show", show);
    }

    public async Task FetchAsync(string root, CancellationToken cancellationToken)
    {
        var fetch = await _git.RunAsync(root, ["fetch", "--quiet", "--no-recurse-submodules"], NetworkTimeout, cancellationToken).ConfigureAwait(false);
        if (!fetch.Succeeded)
        {
            throw Failed("fetch", fetch);
        }
    }

    public async Task FastForwardAsync(string root, CancellationToken cancellationToken)
    {
        var merge = await RunAsync(root, ["merge", "--ff-only", "--quiet", "@{u}"], cancellationToken).ConfigureAwait(false);
        if (!merge.Succeeded)
        {
            throw Failed("merge", merge);
        }
    }

    private Task<GitOutput> RunAsync(string root, string[] arguments, CancellationToken cancellationToken) =>
        _git.RunAsync(root, arguments, LocalTimeout, cancellationToken);

    private static string Literal(string relativePath) => ":(literal)" + relativePath;

    private static List<GitChange> ParseNameStatus(string output)
    {
        var fields = output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
        var changes = new List<GitChange>();
        for (var i = 0; i + 1 < fields.Length; i += 2)
        {
            var kind = fields[i][0] switch
            {
                'A' => GitChangeKind.Added,
                'D' => GitChangeKind.Deleted,
                _ => GitChangeKind.Modified,
            };
            changes.Add(new GitChange(fields[i + 1], kind));
        }

        return changes;
    }

    private static int ParseCount(string[] parts, int index) =>
        parts.Length > index && int.TryParse(parts[index], NumberStyles.None, CultureInfo.InvariantCulture, out var value) ? value : 0;

    private static GitException Failed(string command, GitOutput output) =>
        new($"git {command} failed: {output.FirstErrorLine}");
}

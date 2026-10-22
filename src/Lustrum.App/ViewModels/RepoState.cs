using Lustrum.Core.Git;

namespace Lustrum.App.ViewModels;

/// <summary>What Lustrum has done with a repository during this session.</summary>
public abstract record RepoState
{
    public static RepoState NotChecked { get; } = new NotCheckedRepo();

    public virtual bool IsBusy => false;

    public abstract string Note { get; }
}

public sealed record NotCheckedRepo : RepoState
{
    public override string Note => "Counts are as of the last fetch; Check for updates fetches now";
}

public sealed record CheckingRepo : RepoState
{
    public override bool IsBusy => true;

    public override string Note => "Checking for updates…";
}

public sealed record UpdatingRepo : RepoState
{
    public override bool IsBusy => true;

    public override string Note => "Updating…";
}

/// <summary>
/// A fetched repository. Incoming maps a skill's relative path to the upstream commits that touch it; it is null when
/// the counts could not be taken.
/// </summary>
public sealed record CheckedRepo(GitTracking Tracking, DateTimeOffset At, IReadOnlyDictionary<string, int>? Incoming = null) : RepoState
{
    /// <summary>
    /// Commits that touch the skill. Without a count for it, a skills repository falls back to its whole behind count,
    /// which is what Update would pull, and a project repository to none, since its commits mostly concern other files.
    /// </summary>
    public int IncomingFor(SkillLocation location) =>
        Incoming?.TryGetValue(location.RelativePath, out var count) == true
            ? count
            : location.Repository.Role == RepositoryRole.SkillsOnly ? Tracking.Behind : 0;

    public bool HasCountsFor(IEnumerable<string> relativePaths) =>
        Tracking.Behind == 0 || (Incoming is { } counts && relativePaths.All(counts.ContainsKey));

    public override string Note => $"Checked for updates at {At.ToLocalTime():HH:mm}";
}

public sealed record FailedRepo(string Message) : RepoState
{
    public override string Note => $"Check failed: {Message}";
}

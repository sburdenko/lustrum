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

public sealed record CheckedRepo(GitTracking Tracking, DateTimeOffset At) : RepoState
{
    public override string Note => $"Checked for updates at {At.ToLocalTime():HH:mm}";
}

public sealed record FailedRepo(string Message) : RepoState
{
    public override string Note => $"Check failed: {Message}";
}

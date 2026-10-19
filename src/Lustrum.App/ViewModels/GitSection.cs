using Lustrum.Core.Git;

namespace Lustrum.App.ViewModels;

public sealed record GitChangeRow(string Code, string Path);

/// <summary>What the card's GIT section shows for a skill, built from its git state and the repository's session state.</summary>
public sealed record GitSection(
    bool IsVisible,
    string Headline,
    string? LastCommit = null,
    IReadOnlyList<GitChangeRow>? Changes = null,
    string? RepositoryText = null,
    SkillRepository? Repository = null,
    bool CanCheck = false,
    bool ShowUpdate = false,
    bool CanUpdate = false,
    string? CheckNote = null,
    string? UpdateNote = null)
{
    private const int MaxChanges = 8;

    public static GitSection Hidden { get; } = new(false, string.Empty);

    public IReadOnlyList<GitChangeRow> ChangeRows => Changes ?? [];

    public bool HasChanges => ChangeRows.Count > 0;

    public string ChangesTitle => ChangeRows.Count == 1 ? "1 uncommitted change" : $"{ChangeRows.Count} uncommitted changes";

    public bool HasLastCommit => LastCommit is not null;

    public bool HasRepositoryText => RepositoryText is not null;

    public bool ShowActions => Repository is not null;

    public bool HasCheckNote => CheckNote is not null;

    public bool HasUpdateNote => UpdateNote is not null;

    public static GitSection From(GitCardState state, RepoState repo) => state switch
    {
        LoadingGit => new(true, "Reading git…"),
        NoRepository => new(true, "Not in a git repository"),
        FailedGit failed => new(true, failed.Message),
        ReadyGit ready => Ready(ready.Report, repo),
        _ => Hidden,
    };

    private static GitSection Ready(SkillGitReport report, RepoState repo)
    {
        var skillsOnly = report.Repository.Role == RepositoryRole.SkillsOnly;
        return new GitSection(
            true,
            HeadlineFor(report.Tracking),
            report.LastCommit is { } commit ? $"Last commit {commit.ShortHash} · {commit.Subject} · {commit.Author}, {commit.When.ToLocalTime():d MMM yyyy}" : "No commits yet",
            report.Changes.Take(MaxChanges).Select(c => new GitChangeRow(Code(c.Kind), c.Path)).ToList(),
            $"{report.Repository.Root} · {(skillsOnly ? "skills repository" : $"part of the {report.Repository.Name} repository")}",
            report.Repository,
            CanCheck: !repo.IsBusy && report.Tracking.HasUpstream,
            ShowUpdate: skillsOnly,
            CanUpdate: !repo.IsBusy && report.Update.CanUpdate,
            CheckNote: report.Tracking.HasUpstream || repo.IsBusy ? repo.Note : null,
            UpdateNote: UpdateNoteFor(report));
    }

    private static string HeadlineFor(GitTracking tracking)
    {
        if (tracking.Branch is not { } branch)
        {
            return "Detached HEAD";
        }

        if (tracking.Upstream is not { } upstream)
        {
            return $"{branch} · no upstream";
        }

        var parts = new List<string>();
        if (tracking.Behind > 0)
        {
            parts.Add($"{tracking.Behind} behind");
        }

        if (tracking.Ahead > 0)
        {
            parts.Add($"{tracking.Ahead} ahead");
        }

        return $"{branch} → {upstream} · {(parts.Count == 0 ? "up to date" : string.Join(", ", parts))}";
    }

    private static string? UpdateNoteFor(SkillGitReport report) => report.Update switch
    {
        UpdateAvailable available => available.Behind == 1 ? "1 new commit to pull" : $"{available.Behind} new commits to pull",
        UpdateBlocked blocked when report.Repository.Role == RepositoryRole.Containing || report.Tracking.Behind > 0 => blocked.Reason,
        _ => null,
    };

    private static string Code(GitChangeKind kind) => kind switch
    {
        GitChangeKind.Added => "A",
        GitChangeKind.Deleted => "D",
        GitChangeKind.Renamed => "R",
        GitChangeKind.Untracked => "?",
        GitChangeKind.Conflicted => "!",
        _ => "M",
    };
}

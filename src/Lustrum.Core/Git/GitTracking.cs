namespace Lustrum.Core.Git;

/// <summary>
/// Where the checked-out branch stands against its upstream. Branch is null on a detached HEAD and Upstream is
/// null when the branch tracks nothing; the counts are relative to the upstream as of the last fetch.
/// </summary>
public sealed record GitTracking(string? Branch, string? Upstream, int Ahead, int Behind)
{
    public static GitTracking Detached { get; } = new(null, null, 0, 0);

    public bool HasUpstream => Branch is not null && Upstream is not null;
}

using Lustrum.Core.Git;

namespace Lustrum.App.ViewModels;

public abstract record GitCardState
{
    public static GitCardState Hidden { get; } = new HiddenGit();

    public static GitCardState Loading { get; } = new LoadingGit();

    public static GitCardState NotInRepository { get; } = new NoRepository();
}

public sealed record HiddenGit : GitCardState;

public sealed record LoadingGit : GitCardState;

public sealed record NoRepository : GitCardState;

public sealed record FailedGit(string Message) : GitCardState;

public sealed record ReadyGit(SkillGitReport Report) : GitCardState;

using FluentIcons.Common;
using Lustrum.Core.Problems;

namespace Lustrum.App.ViewModels;

public sealed class ProblemItemViewModel(Problem problem)
{
    public Problem Problem { get; } = problem;

    public string Subject => Problem.Subject;

    public string Path => Problem.Path;

    public string Message => Problem.Kind switch
    {
        ProblemKind.BrokenSkill => WithDetail(HealthPresentation.For(Problem.Skill!.Health).Text),
        ProblemKind.LongDescription => $"Description is over 1024 characters ({Problem.Detail})",
        ProblemKind.MissingProject => "Project folder no longer exists",
        _ => Problem.Detail ?? "Could not be read",
    };

    public bool IsError => Problem.Kind is ProblemKind.BrokenSkill or ProblemKind.ReadIssue;

    public bool IsWarning => !IsError;

    public string Badge => Problem.Kind switch
    {
        ProblemKind.MissingProject => "GONE",
        ProblemKind.LongDescription => "LONG",
        ProblemKind.ReadIssue => "READ",
        _ => "ERR",
    };

    public Symbol Icon => Problem.Kind switch
    {
        ProblemKind.MissingProject => Symbol.FolderProhibited,
        ProblemKind.ReadIssue => Symbol.DocumentError,
        ProblemKind.LongDescription => Symbol.Warning,
        _ => Symbol.ErrorCircle,
    };

    public bool Matches(string text) =>
        text.Length == 0
        || Subject.Contains(text, StringComparison.OrdinalIgnoreCase)
        || Path.Contains(text, StringComparison.OrdinalIgnoreCase)
        || Message.Contains(text, StringComparison.OrdinalIgnoreCase);

    private string WithDetail(string label) => Problem.Detail is null ? label : $"{label}: {Problem.Detail}";
}

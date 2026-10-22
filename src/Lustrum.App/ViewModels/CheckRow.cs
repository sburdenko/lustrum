using Lustrum.Core.Models;
using Lustrum.Core.Problems;

namespace Lustrum.App.ViewModels;

/// <summary>One linter finding on the card, with the short mark the Problems list uses.</summary>
public sealed record CheckRow(string Badge, string Message)
{
    public static IReadOnlyList<CheckRow> For(Skill skill) =>
        skill.Findings.Select(f => new CheckRow(ProblemItemViewModel.BadgeFor(ProblemCollector.KindOf(f.Kind)), f.Message)).ToList();
}

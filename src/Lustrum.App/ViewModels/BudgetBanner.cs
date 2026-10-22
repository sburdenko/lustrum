using Lustrum.Core.Budget;

namespace Lustrum.App.ViewModels;

/// <summary>The Effective view's skill-list budget: a short line above the list and the full sentence as its tooltip.</summary>
public sealed record BudgetBanner(BudgetSummary Summary)
{
    public string Text => string.Join(" · ", Parts(
        $"≈{Summary.ListedTokens:N0} TOKENS",
        $"{Summary.ListedSkills} LISTED",
        Summary.HiddenSkills > 0 ? $"{Summary.HiddenSkills} HIDDEN" : null,
        Summary.OnDemandSkills > 0 ? $"+≈{Summary.OnDemandTokens:N0} ON DEMAND" : null));

    public string Tip => string.Concat(Parts(
        $"About {Summary.ListedTokens:N0} tokens of names and descriptions in Claude's skill list in every session, from {Summary.ListedSkills} listed skills",
        Summary.HiddenSkills > 0 ? $"; {Summary.HiddenSkills} hidden by their visibility" : null,
        Summary.OnDemandSkills > 0 ? $"; {Summary.OnDemandSkills} more on demand, about {Summary.OnDemandTokens:N0} tokens" : null,
        ". An estimate at about four characters per token."));

    private static IEnumerable<string> Parts(params string?[] parts) => parts.OfType<string>();
}

using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;

namespace Lustrum.Core.Budget;

/// <summary>
/// Estimates how much of Claude's context a skill takes. Every session lists each loaded skill to Claude by name and
/// description plus <c>when_to_use</c>, capped at 1,536 characters (by name only for <c>name-only</c>, not at all when
/// hidden or when <c>disable-model-invocation</c> is set); invoking a skill then adds its whole SKILL.md. Claude Code
/// does not document the exact listing text and no Claude tokenizer runs locally, so these are character counts with a
/// rough token estimate of four characters per token, before Claude Code trims the listing to its own budget.
/// </summary>
public static class ContextBudget
{
    private const int CharsPerToken = 4;

    /// <summary>Claude Code's default cap on one entry's description and when_to_use text.</summary>
    public const int EntryCap = 1536;

    public static int Tokens(long chars) => (int)((chars + CharsPerToken - 1) / CharsPerToken);

    /// <summary>Characters a skill adds to the list of skills Claude sees in every session.</summary>
    public static int ListedChars(Skill skill, SkillVisibility visibility) => visibility switch
    {
        _ when KeptFromClaude(skill) => 0,
        SkillVisibility.On => skill.InvocationName.Length + DescriptionChars(skill),
        SkillVisibility.NameOnly => skill.InvocationName.Length,
        _ => 0,
    };

    /// <summary>Whether <c>disable-model-invocation: true</c> keeps the skill out of Claude's list.</summary>
    public static bool KeptFromClaude(Skill skill) =>
        skill.Frontmatter.TryGetValue("disable-model-invocation", out var value) && string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);

    /// <summary>Characters the entry adds to the list in this project; a skill on demand counts once it loads.</summary>
    public static int ListedChars(EffectiveEntry entry) =>
        entry.Availability == Availability.Shadowed ? 0 : ListedChars(entry.Skill, entry.VisibilityValue);

    /// <summary>Characters saved per session by listing the skill by name only.</summary>
    public static int NameOnlySaving(Skill skill, SkillVisibility visibility) =>
        visibility == SkillVisibility.On && !KeptFromClaude(skill) ? DescriptionChars(skill) : 0;

    private static int DescriptionChars(Skill skill)
    {
        var description = skill.Description?.Length ?? 0;
        var whenToUse = skill.Frontmatter.TryGetValue("when_to_use", out var extra) && extra.Length > 0 ? extra.Length + 1 : 0;
        return Math.Min(description + whenToUse, EntryCap);
    }

    public static BudgetSummary Summarize(EffectiveSet set)
    {
        var active = set.Entries.Where(e => e.Availability == Availability.Active).ToList();
        var listed = active.Where(e => ListedChars(e) > 0).ToList();
        var onDemand = set.Entries.Where(e => e.Availability == Availability.OnDemand).ToList();
        return new BudgetSummary(
            listed.Count,
            listed.Sum(ListedChars),
            onDemand.Count,
            onDemand.Sum(ListedChars),
            active.Count - listed.Count);
    }
}

/// <summary>A project's skill list: what every session carries, what joins on demand, what is hidden.</summary>
public sealed record BudgetSummary(int ListedSkills, int ListedChars, int OnDemandSkills, int OnDemandChars, int HiddenSkills)
{
    public int ListedTokens => ContextBudget.Tokens(ListedChars);

    public int OnDemandTokens => ContextBudget.Tokens(OnDemandChars);
}

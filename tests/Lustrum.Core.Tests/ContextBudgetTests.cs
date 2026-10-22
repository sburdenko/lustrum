using FluentAssertions;
using Lustrum.Core.Budget;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;

namespace Lustrum.Core.Tests;

public class ContextBudgetTests
{
    private static readonly SkillSource Global = new("global", SourceKind.Global, "Global", "/g", null, true);

    private static Skill Skill(string name, string description, Dictionary<string, string>? frontmatter = null) =>
        new($"global#{name}", name, name, description, SkillKind.Skill, Global, $"/g/skills/{name}/SKILL.md", null,
            frontmatter ?? new Dictionary<string, string>(), [], [], 4000, DateTimeOffset.UnixEpoch, Health.Ok, []);

    [Fact]
    public void Listed_characters_follow_visibility()
    {
        var skill = Skill("deploy", new string('x', 94));

        ContextBudget.ListedChars(skill, SkillVisibility.On).Should().Be(100);
        ContextBudget.ListedChars(skill, SkillVisibility.NameOnly).Should().Be(6);
        ContextBudget.ListedChars(skill, SkillVisibility.UserInvocableOnly).Should().Be(0);
        ContextBudget.ListedChars(skill, SkillVisibility.Off).Should().Be(0);
        ContextBudget.NameOnlySaving(skill, SkillVisibility.On).Should().Be(94);
        ContextBudget.NameOnlySaving(skill, SkillVisibility.NameOnly).Should().Be(0);
    }

    [Fact]
    public void When_to_use_is_appended_and_the_entry_is_capped_like_claude_code_does()
    {
        var withHint = Skill("deploy", "1234", new() { ["when_to_use"] = "12345" });
        var huge = Skill("big", new string('x', 2000));

        ContextBudget.ListedChars(withHint, SkillVisibility.On).Should().Be(6 + 4 + 1 + 5);
        ContextBudget.ListedChars(huge, SkillVisibility.On).Should().Be(3 + ContextBudget.EntryCap);
    }

    [Fact]
    public void Disable_model_invocation_keeps_a_skill_out_of_the_list()
    {
        var manual = Skill("ship", "Ship it", new() { ["disable-model-invocation"] = "true" });

        ContextBudget.ListedChars(manual, SkillVisibility.On).Should().Be(0);
        ContextBudget.ListedChars(manual, SkillVisibility.NameOnly).Should().Be(0);
        ContextBudget.NameOnlySaving(manual, SkillVisibility.On).Should().Be(0);
    }

    [Fact]
    public void Tokens_round_up_at_four_characters_each()
    {
        ContextBudget.Tokens(0).Should().Be(0);
        ContextBudget.Tokens(1).Should().Be(1);
        ContextBudget.Tokens(8).Should().Be(2);
        ContextBudget.Tokens(9).Should().Be(3);
    }

    [Fact]
    public void A_summary_counts_listed_hidden_and_on_demand_skills_but_not_shadowed_ones()
    {
        var listed = Skill("a", "12345678");
        var hidden = Skill("b", "desc");
        var shadowed = Skill("c", "desc");
        var later = Skill("d", "1234");
        var off = new ResolvedVisibility(SkillVisibility.Off, null);
        var set = new EffectiveSet(Global, [
            new EffectiveEntry(listed, Availability.Active, null, []),
            new EffectiveEntry(hidden, Availability.Active, null, [], off),
            new EffectiveEntry(shadowed, Availability.Shadowed, listed, []),
            new EffectiveEntry(later, Availability.OnDemand, null, [], LoadsUnder: "apps/x"),
        ]);

        var summary = ContextBudget.Summarize(set);

        summary.Should().Be(new BudgetSummary(1, 9, 1, 5, 1));
        summary.ListedTokens.Should().Be(3);
        ContextBudget.ListedChars(set.Entries[2]).Should().Be(0);
        ContextBudget.ListedChars(set.Entries[3]).Should().Be(5);
    }
}

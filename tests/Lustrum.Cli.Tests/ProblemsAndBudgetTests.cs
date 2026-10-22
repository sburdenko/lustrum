using FluentAssertions;
using Lustrum.Core.Budget;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;
using Lustrum.Core.Problems;

namespace Lustrum.Cli.Tests;

public class ProblemsAndBudgetTests
{
    private static readonly SkillSource Global = new("global", SourceKind.Global, "Global", "/h/.claude", null, true);

    private static Skill Skill(string name, string description) => new(
        $"global#{name}", name, name, description, SkillKind.Skill, Global, $"/x/{name}/SKILL.md",
        null, new Dictionary<string, string>(), [], [], 1, DateTimeOffset.UnixEpoch, Health.Ok, []);

    [Fact]
    public void Problems_render_as_a_table_with_a_count()
    {
        var skill = Skill("risky", "d");
        var problems = new[]
        {
            new Problem(ProblemKind.MissingReference, "risky", skill.Path, "Links to scripts/run.sh, which is not in the skill folder", skill),
            new Problem(ProblemKind.BroadPermission, "risky", skill.Path, "allowed-tools entry Bash lets the skill run any shell command without asking", skill),
        };

        var lines = ProblemsCommand.Render(problems).ReplaceLineEndings("\n").Split('\n');

        lines[0].Should().MatchRegex(@"^KIND\s+SUBJECT\s+MESSAGE$");
        lines[1].Should().StartWith("link").And.EndWith("which is not in the skill folder");
        lines[2].Should().StartWith("wide");
        lines[^1].Should().Be("2 problems");
    }

    [Fact]
    public void No_problems_says_so() =>
        ProblemsCommand.Render([]).Should().Be("No problems found.");

    [Fact]
    public void Budget_table_lists_the_costliest_skill_first_with_its_tokens()
    {
        var entries = new[]
        {
            new EffectiveEntry(Skill("small", "x"), Availability.Active, null, []),
            new EffectiveEntry(Skill("big", new string('d', 397)), Availability.Active, null, []),
            new EffectiveEntry(Skill("quiet", "desc"), Availability.Active, null, [], new ResolvedVisibility(SkillVisibility.Off, null)),
        };

        var lines = SkillTable.RenderBudget(entries).ReplaceLineEndings("\n").Split('\n');

        lines[0].Should().Contain("IN PROJECT  ≈TOKENS");
        lines[1].Should().MatchRegex(@"^big\s.*\s100\s+d+…$");
        lines[2].Should().MatchRegex(@"^small\s.*\s2\s+x$");
        lines[3].Should().MatchRegex(@"^quiet\s.*\s0\s+desc$");
    }

    [Fact]
    public void Budget_line_mentions_hidden_and_on_demand_skills_only_when_there_are_any()
    {
        SkillTable.BudgetLine(new BudgetSummary(4, 4000, 0, 0, 0))
            .Should().Be("≈1,000 tokens in Claude's skill list from 4 listed skills. Estimate: about four characters per token.");
        SkillTable.BudgetLine(new BudgetSummary(2, 80, 1, 40, 3))
            .Should().Be("≈20 tokens in Claude's skill list from 2 listed skills, 3 hidden; 1 more on demand, ≈10 tokens. Estimate: about four characters per token.");
    }
}

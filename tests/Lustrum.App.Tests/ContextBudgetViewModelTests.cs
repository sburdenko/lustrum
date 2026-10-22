using FluentAssertions;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using Lustrum.Core.Indexing;
using Lustrum.Core.Linting;
using Lustrum.Core.Models;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.App.Tests;

public class ContextBudgetViewModelTests
{
    private static async Task<MainViewModel> Scanned(ScanResult? result = null)
    {
        var scan = result ?? TestData.WithShadowing();
        var vm = new MainViewModel(
            (_, _) => Task.FromResult(scan),
            _ => Task.FromResult(UsageReport.Empty),
            new AppServices(new FakeShell(), new FakeClipboard(), new FakeSettings(LustrumSettings.Default), new FakeTheme(), new FakeSkillGit(), new FakeSkillWriter()));
        await vm.RescanCommand.ExecuteAsync(null);
        return vm;
    }

    private static NavNode Node(MainViewModel vm, string title) =>
        Flatten(vm.NavRoots).First(n => n.Title == title);

    private static IEnumerable<NavNode> Flatten(IEnumerable<NavNode> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));

    private static async Task<MainViewModel> Effective(ScanResult? result = null)
    {
        var vm = await Scanned(result);
        vm.SelectedNode = Node(vm, "app");
        vm.SelectedProjectView = vm.ProjectViews.Single(v => v.View == ProjectView.Effective);
        return vm;
    }

    [Fact]
    public async Task The_effective_set_shows_the_skill_list_budget_and_each_row_cost()
    {
        var vm = await Effective();

        vm.Budget!.Text.Should().Be("≈13 TOKENS · 3 LISTED");
        vm.Budget.Tip.Should().Be("About 13 tokens of names and descriptions in Claude's skill list in every session, from 3 listed skills. An estimate at about four characters per token.");
        vm.HasBudget.Should().BeTrue();
        vm.VisibleSkills.Single(s => s.IsShadowed).CostText.Should().Be("≈0");
        vm.VisibleSkills.Single(s => s.Skill.FullName == "notes").CostText.Should().Be("≈5");
    }

    [Fact]
    public void The_banner_mentions_hidden_and_on_demand_skills_only_when_there_are_any()
    {
        var banner = new BudgetBanner(new Lustrum.Core.Budget.BudgetSummary(20, 1424, 1, 72, 2));

        banner.Text.Should().Be("≈356 TOKENS · 20 LISTED · 2 HIDDEN · +≈18 ON DEMAND");
        banner.Tip.Should().Contain("; 2 hidden by their visibility; 1 more on demand, about 18 tokens.");
    }

    [Fact]
    public async Task Other_views_have_no_budget_line_or_row_cost()
    {
        var vm = await Scanned();

        vm.SelectedNode = Node(vm, "Global");

        vm.HasBudget.Should().BeFalse();
        vm.VisibleSkills.Should().OnlyContain(s => !s.HasCost);
    }

    [Fact]
    public async Task Sorting_by_context_cost_puts_the_costliest_listed_skill_first()
    {
        var scan = TestData.Scan(
            [TestData.Global],
            [TestData.Skill(TestData.Global, "short", "x"), TestData.Skill(TestData.Global, "long", new string('d', 300))]);
        var vm = await Scanned(scan);

        vm.SelectedSort = vm.Sorts.Single(s => s.Mode == SortMode.ContextCost);

        vm.VisibleSkills.Select(s => s.Name).Should().Equal("long", "short");
    }

    [Fact]
    public async Task The_card_explains_the_context_cost_and_suggests_name_only_for_a_long_description()
    {
        var skill = TestData.Skill(TestData.Global, "verbose", new string('d', 900));
        var vm = await Scanned(TestData.Scan([TestData.Global], [skill]));

        vm.SelectedSkill = vm.VisibleSkills.Single();

        var context = vm.Card!.Context!;
        context.Listed.Should().Be("Listed to Claude in every session: 907 characters, ≈227 tokens");
        context.Invoked.Should().Be("Invoking it adds SKILL.md: 10 bytes, ≈3 tokens");
        context.Hint.Should().Be("Name only would save ≈225 tokens per session");
    }

    [Fact]
    public async Task A_skill_kept_for_slash_use_only_is_not_listed()
    {
        var manual = TestData.Skill(TestData.Global, "ship", new string('d', 900)) with
        {
            Frontmatter = new Dictionary<string, string> { ["disable-model-invocation"] = "true" },
        };
        var vm = await Scanned(TestData.Scan([TestData.Global], [manual]));

        vm.SelectedSkill = vm.VisibleSkills.Single();

        vm.Card!.Context!.Listed.Should().Be("Not listed to Claude: disable-model-invocation keeps it for /name only");
        vm.Card.Context.HasHint.Should().BeFalse();
    }

    [Fact]
    public async Task A_shadowed_copy_is_not_listed_and_gets_no_hint()
    {
        var vm = await Effective();

        vm.SelectedSkill = vm.VisibleSkills.Single(s => s.IsShadowed);

        vm.Card!.Context!.Listed.Should().Be("Not listed to Claude here: another copy takes the name");
        vm.Card.Context.HasHint.Should().BeFalse();
    }

    [Fact]
    public async Task Linter_findings_appear_on_the_card_and_in_the_problems_list()
    {
        var skill = TestData.Skill(TestData.Global, "risky") with
        {
            Findings =
            [
                new SkillFinding(FindingKind.MissingFile, "scripts/run.sh", "Links to scripts/run.sh, which is not in the skill folder"),
                new SkillFinding(FindingKind.BroadTool, "Bash", "allowed-tools entry Bash lets the skill run any shell command without asking"),
            ],
        };
        var vm = await Scanned(TestData.Scan([TestData.Global], [skill]));

        vm.SelectedSkill = vm.VisibleSkills.Single();

        vm.Card!.HasChecks.Should().BeTrue();
        vm.Card.Checks.Select(c => c.Badge).Should().Equal("LINK", "WIDE");
        vm.SelectedNode = vm.NavRoots.Single(n => n.Scope is ProblemsScope);
        vm.VisibleProblems.Select(p => p.Badge).Should().Equal("LINK", "WIDE");
    }
}

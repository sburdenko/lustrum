using FluentAssertions;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using Lustrum.Core.Models;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.App.Tests;

public class IndexLayoutTests
{
    private UsageReport _usage = UsageReport.Empty;

    private async Task<MainViewModel> Scanned(ScanResult? scan = null)
    {
        var result = scan ?? TestData.WithShadowing();
        var vm = new MainViewModel(
            (_, _) => Task.FromResult(result),
            _ => Task.FromResult(_usage),
            new AppServices(new FakeShell(), new FakeClipboard(), new FakeSettings(LustrumSettings.Default), new FakeTheme(), new FakeSkillGit(), new FakeSkillWriter()));
        await vm.RescanCommand.ExecuteAsync(null);
        return vm;
    }

    private static NavNode Node(MainViewModel vm, string title) =>
        vm.NavSections.SelectMany(s => new[] { s.Node }.Concat(s.Children.Select(c => c.Node))).First(n => n.Title == title);

    [Fact]
    public async Task Sections_carry_their_keys_and_open_only_around_the_selection()
    {
        var vm = await Scanned();

        vm.NavSections.Select(s => s.Key).Should().Equal("A", "G", "P", "L", "!");
        vm.NavSections.Single(s => s.IsSelected).Title.Should().Be("ALL");
        vm.NavSections.Should().OnlyContain(s => !s.IsOpen || s.IsSelected);

        vm.SelectNodeCommand.Execute(Node(vm, "app"));

        var projects = vm.NavSections.Single(s => s.Key == "P");
        projects.IsOpen.Should().BeTrue();
        projects.IsSelected.Should().BeFalse();
        projects.Children.Single(c => c.IsSelected).Node.Title.Should().Be("app");
        projects.Children.Single(c => c.IsSelected).Indent.Should().Be(34);
    }

    [Fact]
    public async Task Keys_jump_to_sections()
    {
        var vm = await Scanned();

        vm.SelectSection("G").Should().BeTrue();
        vm.SelectedNode!.Title.Should().Be("Global");
        vm.SelectSection("S").Should().BeFalse("there are no synced skills in this scan");
    }

    [Fact]
    public async Task The_full_list_is_grouped_by_kind_with_counted_headings()
    {
        var vm = await Scanned();

        vm.ListRows.OfType<ListHeader>().Should().Equal(
            new ListHeader("GLOBAL", 2), new ListHeader("PROJECTS", 2), new ListHeader("PLUGINS", 1));
        vm.VisibleSkills.Select(s => s.Skill.Source.Kind).Should().BeInAscendingOrder();
        vm.ListRows.OfType<SkillItemViewModel>().Should().Equal(vm.VisibleSkills);
    }

    [Fact]
    public async Task A_search_or_a_single_source_uses_one_heading_named_after_the_view()
    {
        var vm = await Scanned();

        vm.SearchText = "design";

        vm.ListRows.OfType<ListHeader>().Should().Equal(new ListHeader("ALL", 2));
        vm.SelectSection("G");
        vm.SearchText = string.Empty;
        vm.ListRows.OfType<ListHeader>().Should().Equal(new ListHeader("GLOBAL", 2));
    }

    [Fact]
    public async Task Rows_are_numbered_by_scan_order_and_marked()
    {
        var shadowing = TestData.WithShadowing();
        var broken = TestData.Skill(TestData.Global, "zz-broken", "x", Health.ParseError);
        var scan = shadowing with
        {
            Skills = [.. shadowing.Skills, broken],
            Conflicts = [new Conflict("design", shadowing.Skills.Where(s => s.Name == "design").ToList())],
        };
        var vm = await Scanned(scan);

        var rows = vm.VisibleSkills.ToDictionary(r => r.Skill.Id);
        rows[broken.Id].NumberText.Should().Be("006");
        rows[broken.Id].Mark.Should().Be("ERR");
        rows[broken.Id].IsMarkError.Should().BeTrue();
        vm.VisibleSkills.Where(r => r.Name == "design").Should().OnlyContain(r => r.Mark == "×2" && r.IsMarkQuiet);
        vm.VisibleSkills.Single(r => r.Name == "notes").HasMark.Should().BeFalse();
    }

    [Fact]
    public async Task j_and_k_move_through_the_visible_skills_and_stop_at_the_ends()
    {
        var vm = await Scanned();

        vm.MoveSelection(1);
        vm.SelectedSkill.Should().Be(vm.VisibleSkills[0]);
        vm.MoveSelection(-1);
        vm.SelectedSkill.Should().Be(vm.VisibleSkills[0]);
        for (var i = 0; i < 10; i++)
        {
            vm.MoveSelection(1);
        }

        vm.SelectedSkill.Should().Be(vm.VisibleSkills[^1]);
        vm.Card!.Skill.Should().Be(vm.VisibleSkills[^1].Skill);
    }

    [Fact]
    public async Task In_the_problems_view_j_and_k_move_through_problems()
    {
        var broken = TestData.Skill(TestData.Global, "broken", "x", Health.ParseError);
        var vm = await Scanned(TestData.Scan([TestData.Global, TestData.GhostProject], [broken, TestData.Skill(TestData.Global, "fine")]));
        vm.SelectSection("!");

        vm.MoveSelection(1);
        vm.SelectedProblem!.Subject.Should().Be("broken");
        vm.Card!.Skill.Should().Be(broken);
        vm.MoveSelection(1);
        vm.SelectedProblem!.Subject.Should().Be("gone");
        vm.MoveSelection(1);
        vm.SelectedProblem!.Subject.Should().Be("gone");
    }

    [Fact]
    public async Task Moving_in_an_empty_list_does_nothing()
    {
        var vm = await Scanned();
        vm.SearchText = "nothing matches this";

        vm.MoveSelection(1);

        vm.SelectedSkill.Should().BeNull();
    }

    [Fact]
    public async Task The_synced_section_has_its_key()
    {
        var vm = await Scanned(TestData.Scan([TestData.Global, TestData.Account], [TestData.Skill(TestData.Account, "xlsx")]));

        vm.NavSections.Select(s => s.Key).Should().Contain("S");
        vm.SelectSection("S").Should().BeTrue();
    }

    [Fact]
    public async Task The_cli_line_follows_the_view()
    {
        var vm = await Scanned();
        vm.CliCommand.Should().Be("list");

        vm.SelectSection("G");
        vm.CliCommand.Should().Be("list --source global");

        vm.SelectSection("!");
        vm.CliCommand.Should().Be("problems");

        vm.SelectNodeCommand.Execute(Node(vm, "app"));
        vm.SelectedProjectView = vm.ProjectViews.Single(v => v.View == ProjectView.Missing);
        vm.CliCommand.Should().Be("list --project /dev/app --missing");

        vm.SelectSection("A");
        vm.Chips.Single(c => c.Label == "broken").IsOn = true;
        vm.SearchText = "design";
        vm.CliCommand.Should().Be("list --broken | grep -iF design");
        vm.SearchText = "it's here";
        vm.CliCommand.Should().Be("list --broken | grep -iF 'it'\\''s here'");
    }

    [Fact]
    public async Task Comparing_shows_the_diff_command()
    {
        var shadowing = TestData.WithShadowing();
        var vm = await Scanned(shadowing with { Conflicts = [new Conflict("design", shadowing.Skills.Where(s => s.Name == "design").ToList())] });
        vm.SelectedSkill = vm.VisibleSkills.First(s => s.Name == "design");

        vm.Card!.SelectedTab = SkillCardViewModel.CompareTab;

        vm.CliCommand.Should().Be("diff design");
    }

    [Fact]
    public async Task The_footer_counts_rows_or_problems()
    {
        var vm = await Scanned();
        vm.FooterStatus.Should().StartWith("5 rows · ");

        vm.SearchText = "notes";
        vm.FooterStatus.Should().StartWith("1 row · ");

        vm.SelectSection("!");
        vm.FooterStatus.Should().StartWith("0 problems · ");
        vm.SearchWatermark.Should().Be("Type to search 5 skills");
    }

    [Fact]
    public async Task The_card_has_a_header_line_badge_and_usage_figures()
    {
        var at = new DateTimeOffset(DateTimeOffset.Now.Year, 3, 10, 12, 0, 0, TimeSpan.Zero);
        _usage = UsageReport.Empty with
        {
            TranscriptCount = 1,
            ByName = new Dictionary<string, SkillUsage> { ["notes"] = new("notes", 3, at, ["/w/app"], 2, 1, [at, at, at]) },
        };
        var vm = await Scanned();

        vm.SelectedSkill = vm.VisibleSkills.Single(s => s.Name == "notes");

        var card = vm.Card!;
        card.HeaderLine.Should().Be("002 / SKILL / GLOBAL");
        card.HealthBadge.Should().Be("OK");
        card.TitleSize.Should().Be(72);
        card.UsageStats.Select(s => (s.Label, s.Value)).Should().Equal(
            ("USES", "3"), ("BY CLAUDE", "2"), ("BY YOU", "1"), ("LAST", at.ToLocalTime().ToString("dd.MM")));
        card.ApplyUsage(vm.UsageState, new DateOnly(at.Year, 3, 12), TimeZoneInfo.Utc);
        card.UsageDays.Should().HaveCount(30);
        card.UsageDays.Count(d => d.IsUsed).Should().Be(1);
        card.UsageDays[27].IsUsed.Should().BeTrue();
        card.HasIssue.Should().BeFalse();
    }

    [Fact]
    public async Task A_broken_skill_card_explains_the_problem_with_the_read_issue()
    {
        var broken = TestData.Skill(TestData.Global, "broken-yaml-skill", "x", Health.ParseError);
        var scan = TestData.Scan([TestData.Global], [broken]) with { Issues = [new ScanIssue(broken.Path, "Line 2: syntax error")] };
        var vm = await Scanned(scan);

        vm.SelectedSkill = vm.VisibleSkills.Single();

        vm.Card!.HealthBadge.Should().Be("PARSE ERROR");
        vm.Card.Issue.Should().Be("Invalid YAML: Line 2: syntax error");
        vm.Card.TitleSize.Should().Be(46);
    }
}

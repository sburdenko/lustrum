using FluentAssertions;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using Lustrum.Core.Models;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.App.Tests;

public sealed class ComparisonViewModelTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lustrum-compare-" + Guid.NewGuid().ToString("N"));

    public ComparisonViewModelTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        for (var attempt = 0; attempt < 20 && Directory.Exists(_dir); attempt++)
        {
            try
            {
                Directory.Delete(_dir, recursive: true);
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }
    }

    private string File(string name, string content)
    {
        var path = Path.Combine(_dir, name + ".md");
        System.IO.File.WriteAllText(path, content);
        return path;
    }

    private async Task<MainViewModel> Scanned(string globalText, string projectText)
    {
        var global = TestData.Skill(TestData.Global, "review") with { Path = File("global", globalText) };
        var local = TestData.Skill(TestData.Project, "review") with { Path = File("project", projectText) };
        var scan = TestData.Scan([TestData.Global, TestData.Project], [global, local]) with
        {
            Conflicts = [new Conflict("review", [global, local])],
        };
        var vm = new MainViewModel(
            (_, _) => Task.FromResult(scan),
            _ => Task.FromResult(UsageReport.Empty),
            new AppServices(new FakeShell(), new FakeClipboard(), new FakeSettings(LustrumSettings.Default), new FakeTheme()));
        await vm.RescanCommand.ExecuteAsync(null);
        vm.SelectedSkill = vm.VisibleSkills.Single(s => s.Skill.Source.Kind == SourceKind.Global);
        return vm;
    }

    private static async Task Loaded(ComparisonViewModel comparison)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while ((comparison.IsLoading || comparison.Summary.Length == 0) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(20);
        }
    }

    [Fact]
    public async Task Compare_button_opens_the_compare_tab_with_that_copy()
    {
        var vm = await Scanned("same\nmine\n", "same\ntheirs\nextra\n");
        var card = vm.Card!;

        card.CompareWithCommand.Execute(card.Conflicts.Single());
        await Loaded(card.Comparison!);

        card.SelectedTab.Should().Be(SkillCardViewModel.CompareTab);
        card.Comparison!.Target!.Label.Should().Be("app");
        card.Comparison.Summary.Should().Be("1 line only in this copy, 2 lines only in app");
        card.Comparison.Rows.Select(r => r.Prefix + r.Text).Should().Equal(" same", "-mine", "+theirs", "+extra");
    }

    [Fact]
    public async Task Opening_the_compare_tab_picks_the_first_other_copy()
    {
        var vm = await Scanned("a\n", "b\n");
        var card = vm.Card!;

        card.SelectedTab = SkillCardViewModel.CompareTab;
        await Loaded(card.Comparison!);

        card.Comparison!.Target.Should().NotBeNull();
        card.Comparison.Rows.Should().HaveCount(2);
    }

    [Fact]
    public async Task Identical_copies_say_so()
    {
        var vm = await Scanned("same\n", "same\r\n");
        var card = vm.Card!;

        card.CompareWithCommand.Execute(card.Conflicts.Single());
        await Loaded(card.Comparison!);

        card.Comparison!.Summary.Should().Be("Identical to the copy in app");
    }

    [Fact]
    public async Task Unreadable_copy_is_reported()
    {
        var vm = await Scanned("a\n", "b\n");
        var card = vm.Card!;
        System.IO.File.Delete(Path.Combine(_dir, "project.md"));

        card.CompareWithCommand.Execute(card.Conflicts.Single());
        await Loaded(card.Comparison!);

        card.Comparison!.Summary.Should().StartWith("Could not compare:");
        card.Comparison.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task The_open_tab_is_kept_when_moving_to_another_skill()
    {
        var vm = await Scanned("a\n", "b\n");
        vm.Card!.SelectedTab = 1;

        vm.SelectedSkill = vm.VisibleSkills.Single(s => s.Skill.Source.Kind == SourceKind.Project);

        vm.Card!.SelectedTab.Should().Be(1);
    }

    [Fact]
    public async Task The_compare_tab_falls_back_to_overview_for_a_skill_without_copies()
    {
        var vm = await Scanned("a\n", "b\n");
        vm.Card!.SelectedTab = SkillCardViewModel.CompareTab;

        vm.SelectedSkill = new SkillItemViewModel(TestData.Skill(TestData.Global, "solo"));

        vm.Card!.SelectedTab.Should().Be(0);
    }

    [Fact]
    public async Task Unexpected_read_errors_are_reported_and_old_rows_cleared()
    {
        var vm = await Scanned("a\n", "b\n");
        var comparison = vm.Card!.Comparison!;
        vm.Card.CompareWithCommand.Execute(vm.Card.Conflicts.Single());
        await Loaded(comparison);
        comparison.Rows.Should().NotBeEmpty();

        var broken = new CompareTarget("broken", TestData.Skill(TestData.Project, "review") with { Path = "bad\0path" });
        comparison.Target = broken;
        await Loaded(comparison);

        comparison.Summary.Should().StartWith("Could not compare:");
        comparison.Rows.Should().BeEmpty();
    }

    [Fact]
    public async Task Very_large_diffs_are_capped()
    {
        var left = string.Join('\n', Enumerable.Range(0, ComparisonViewModel.MaxRows + 500).Select(i => $"left {i}"));
        var right = string.Join('\n', Enumerable.Range(0, ComparisonViewModel.MaxRows + 500).Select(i => $"right {i}"));
        var vm = await Scanned(left, right);

        vm.Card!.CompareWithCommand.Execute(vm.Card.Conflicts.Single());
        await Loaded(vm.Card.Comparison!);

        var rows = vm.Card.Comparison!.Rows;
        rows.Should().HaveCount(ComparisonViewModel.MaxRows + 1);
        rows[^1].Text.Should().Contain("more lines not shown");
    }

    [Fact]
    public async Task Compare_again_reloads_the_same_copy()
    {
        var vm = await Scanned("a\n", "b\n");
        var card = vm.Card!;
        card.CompareWithCommand.Execute(card.Conflicts.Single());
        await Loaded(card.Comparison!);

        System.IO.File.WriteAllText(Path.Combine(_dir, "project.md"), "a\n");
        card.CompareWithCommand.Execute(card.Conflicts.Single());
        await Task.Delay(100);
        await Loaded(card.Comparison!);

        card.Comparison!.Summary.Should().Be("Identical to the copy in app");
    }

    [Fact]
    public async Task Skill_without_conflicts_has_no_comparison()
    {
        var vm = await Scanned("a\n", "b\n");
        vm.SelectedSkill = null;
        vm.SelectedNode = vm.NavRoots.Single(n => n.Title == "All");

        vm.SelectedSkill = new SkillItemViewModel(TestData.Skill(TestData.Global, "solo"));

        vm.Card!.Comparison.Should().BeNull();
    }
}

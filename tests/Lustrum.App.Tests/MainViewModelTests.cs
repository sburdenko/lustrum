using FluentAssertions;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using Lustrum.Core.Models;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.App.Tests;

public class MainViewModelTests
{
    private readonly FakeShell _shell = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSettings _settings = new(LustrumSettings.Default);
    private readonly FakeTheme _theme = new();

    private MainViewModel Create(Func<CancellationToken, Task<ScanResult>>? scan = null)
    {
        var run = scan ?? (_ => Task.FromResult(TestData.Default()));
        return new MainViewModel((_, token) => run(token), _ => Task.FromResult(UsageReport.Empty), new AppServices(_shell, _clipboard, _settings, _theme));
    }

    private async Task<MainViewModel> Scanned()
    {
        var vm = Create();
        await vm.RescanCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Rescan_fills_list_summary_and_tree()
    {
        var vm = await Scanned();

        vm.VisibleSkills.Should().HaveCount(4);
        vm.NavRoots.Should().HaveCount(5);
        vm.Summary.Should().Be("4 skills · 4 sources · 0 conflicts");
        vm.SelectedNode!.Title.Should().Be("All");
        vm.IsScanning.Should().BeFalse();
    }

    [Fact]
    public async Task Search_filters_visible_skills()
    {
        var vm = await Scanned();

        vm.SearchText = "ship";

        vm.VisibleSkills.Select(s => s.Name).Should().Equal("deploy");
    }

    [Fact]
    public async Task Selecting_a_node_scopes_the_list()
    {
        var vm = await Scanned();

        vm.SelectedNode = vm.NavRoots.Single(n => n.Title == "Global");

        vm.VisibleSkills.Select(s => s.Name).Should().BeEquivalentTo("design", "broken-one");
        vm.ListTitle.Should().Be("Global");
    }

    [Fact]
    public async Task Selecting_a_skill_builds_a_card()
    {
        var vm = await Scanned();

        vm.SelectedSkill = vm.VisibleSkills.First(s => s.Name == "deploy");

        vm.HasCard.Should().BeTrue();
        vm.Card!.Title.Should().Be("deploy");
    }

    [Fact]
    public async Task Narrowing_search_keeps_the_same_card_instance_for_the_same_skill()
    {
        var vm = await Scanned();
        vm.SelectedSkill = vm.VisibleSkills.First(s => s.Name == "deploy");
        var card = vm.Card;

        vm.SearchText = "dep";

        vm.Card.Should().BeSameAs(card);
        vm.SelectedSkill!.Name.Should().Be("deploy");
    }

    [Fact]
    public async Task Selection_is_cleared_when_search_hides_the_skill()
    {
        var vm = await Scanned();
        vm.SelectedSkill = vm.VisibleSkills.First(s => s.Name == "deploy");

        vm.SearchText = "design";

        vm.SelectedSkill.Should().BeNull();
        vm.HasCard.Should().BeFalse();
    }

    [Fact]
    public async Task Copy_and_reveal_commands_use_services()
    {
        var vm = await Scanned();
        vm.SelectedSkill = vm.VisibleSkills.First(s => s.Name == "ui");

        vm.OpenFolderCommand.Execute(null);
        await vm.CopyPathCommand.ExecuteAsync(null);
        await vm.CopyInvocationCommand.ExecuteAsync(null);

        _shell.Revealed.Should().ContainSingle().Which.Should().EndWith("/ui/SKILL.md");
        _clipboard.Texts.Should().HaveCount(2);
        _clipboard.Texts[1].Should().Be("unity:ui");
    }

    [Fact]
    public void Commands_are_disabled_without_selection()
    {
        var vm = Create();

        vm.OpenFolderCommand.CanExecute(null).Should().BeFalse();
        vm.CopyPathCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Scan_failure_is_reported_not_thrown()
    {
        var vm = Create(_ => throw new IOException("disk gone"));

        await vm.RescanCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Contain("disk gone");
        vm.IsScanning.Should().BeFalse();
    }

    [Fact]
    public async Task Toggling_empty_sources_rebuilds_tree_and_keeps_selection()
    {
        var vm = await Scanned();
        vm.SelectedNode = vm.NavRoots.Single(n => n.Title == "Global");

        vm.ShowEmptySources = true;

        vm.SelectedNode!.Title.Should().Be("Global");
        vm.NavRoots.Single(n => n.Title == "Projects").Children.Should().HaveCount(3);
    }

    [Fact]
    public async Task Stale_scan_result_never_overwrites_a_newer_one()
    {
        var slow = new TaskCompletionSource<ScanResult>();
        var calls = 0;
        var vm = Create(_ => ++calls == 1 ? slow.Task : Task.FromResult(TestData.Default()));

        var first = vm.RescanCommand.ExecuteAsync(null);
        await vm.RescanCommand.ExecuteAsync(null);
        slow.SetResult(TestData.Scan([TestData.Global], [TestData.Skill(TestData.Global, "stale-only")]));
        await first;

        vm.VisibleSkills.Select(s => s.Name).Should().NotContain("stale-only");
        vm.VisibleSkills.Should().HaveCount(4);
        vm.IsScanning.Should().BeFalse();
    }

    [Fact]
    public async Task Sequential_rescans_do_not_touch_a_disposed_cancellation_source()
    {
        var vm = await Scanned();

        var act = async () => await vm.RescanCommand.ExecuteAsync(null);

        await act.Should().NotThrowAsync();
        vm.VisibleSkills.Should().HaveCount(4);
    }

    [Fact]
    public async Task Unexpected_scan_exception_is_reported_not_thrown()
    {
        var vm = Create(_ => throw new ArgumentException("duplicate key"));

        await vm.RescanCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Contain("duplicate key");
    }

    [Fact]
    public async Task Shell_failure_is_reported()
    {
        var vm = await Scanned();
        vm.SelectedSkill = vm.VisibleSkills.First();
        _shell.Succeeds = false;

        vm.OpenFolderCommand.Execute(null);

        vm.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Theory]
    [InlineData(0, "0 skills")]
    [InlineData(1, "1 skill")]
    [InlineData(2, "2 skills")]
    [InlineData(21, "21 skills")]
    public async Task Summary_pluralizes_the_skill_count(int count, string expected)
    {
        var skills = Enumerable.Range(0, count).Select(i => TestData.Skill(TestData.Global, $"s{i}")).ToList();
        var vm = Create(_ => Task.FromResult(TestData.Scan([TestData.Global], skills)));

        await vm.RescanCommand.ExecuteAsync(null);

        vm.Summary.Should().StartWith(expected);
    }
}

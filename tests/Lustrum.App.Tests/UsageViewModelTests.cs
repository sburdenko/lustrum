using FluentAssertions;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using Lustrum.Core.Models;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.App.Tests;

public class UsageViewModelTests
{
    private readonly FakeShell _shell = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeSettings _settings = new(LustrumSettings.Default);
    private readonly FakeTheme _theme = new();
    private TaskCompletionSource<UsageReport> _usage = new();
    private int _usageCalls;

    private static readonly DateTimeOffset Old = DateTimeOffset.Parse("2026-09-01T10:00:00Z");
    private static readonly DateTimeOffset Recent = DateTimeOffset.Parse("2026-10-01T10:00:00Z");

    private static UsageReport Report(params SkillUsage[] usages) =>
        UsageReport.Empty with { ByName = usages.ToDictionary(u => u.Name), TranscriptCount = 4 };

    private static ScanResult Scan() => TestData.Scan(
        [TestData.Global],
        [TestData.Skill(TestData.Global, "alpha"), TestData.Skill(TestData.Global, "beta"), TestData.Skill(TestData.Global, "gamma")]);

    private MainViewModel Create() => new(
        (_, _) => Task.FromResult(Scan()),
        _ =>
        {
            _usageCalls++;
            return _usage.Task;
        },
        new AppServices(_shell, _clipboard, _settings, _theme, new FakeSkillGit(), new FakeSkillWriter()));

    private async Task<MainViewModel> WithUsage(UsageReport report)
    {
        var vm = Create();
        await vm.RescanCommand.ExecuteAsync(null);
        _usage.SetResult(report);
        await Task.Delay(50);
        return vm;
    }

    [Fact]
    public async Task Unused_chip_waits_for_usage_then_filters_skills_never_used()
    {
        var vm = Create();
        await vm.RescanCommand.ExecuteAsync(null);
        var unused = vm.Chips.Single(c => c.Label == "unused");
        unused.IsAvailable.Should().BeFalse();

        _usage.SetResult(Report(new SkillUsage("beta", 2, Recent, ["/w"], 2, 0)));
        await Task.Delay(50);
        unused.IsAvailable.Should().BeTrue();
        unused.IsOn = true;

        vm.VisibleSkills.Select(s => s.Name).Should().BeEquivalentTo("alpha", "gamma");
    }

    [Fact]
    public async Task Last_use_sort_puts_recent_first_and_unused_last()
    {
        var vm = await WithUsage(Report(
            new SkillUsage("alpha", 1, Old, [], 1, 0),
            new SkillUsage("gamma", 3, Recent, [], 2, 1)));

        vm.SelectedSort = vm.Sorts.Single(s => s.Mode == SortMode.LastUsed);

        vm.VisibleSkills.Select(s => s.Name).Should().Equal("gamma", "alpha", "beta");
    }

    [Fact]
    public async Task Card_shows_usage_once_it_arrives()
    {
        var vm = Create();
        await vm.RescanCommand.ExecuteAsync(null);
        vm.SelectedSkill = vm.VisibleSkills.Single(s => s.Name == "gamma");
        vm.Card!.UsageText.Should().Be("Reading Claude Code transcripts…");

        _usage.SetResult(Report(new SkillUsage("gamma", 3, Recent, ["/work/app", "/work/site"], 2, 1)));
        await Task.Delay(50);

        vm.Card!.UsageText.Should().StartWith("Used 3 times (2 by Claude, 1 by you), last on ");
        vm.Card.UsageProjects.Should().Be("app, site");
    }

    [Fact]
    public async Task Card_says_when_a_skill_was_never_used()
    {
        var vm = await WithUsage(Report());

        vm.SelectedSkill = vm.VisibleSkills.First();

        vm.Card!.UsageText.Should().Be("No recorded use in Claude Code transcripts");
        vm.Card.HasUsageProjects.Should().BeFalse();
    }

    [Fact]
    public async Task Manual_rescan_refreshes_usage_but_watcher_rescans_do_not()
    {
        var vm = await WithUsage(Report());
        _usageCalls.Should().Be(1);

        await vm.RescanFromWatcherAsync();
        _usageCalls.Should().Be(1);

        _usage = new TaskCompletionSource<UsageReport>();
        _usage.SetResult(Report());
        await vm.RescanCommand.ExecuteAsync(null);
        _usageCalls.Should().Be(2);
    }

    [Fact]
    public async Task Usage_failure_is_reported_and_chip_stays_unavailable()
    {
        var vm = Create();
        await vm.RescanCommand.ExecuteAsync(null);

        _usage.SetException(new IOException("transcripts locked"));
        await Task.Delay(50);

        vm.ErrorMessage.Should().Contain("transcripts locked");
        vm.Chips.Single(c => c.Label == "unused").IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Card_says_when_transcripts_could_not_be_read()
    {
        var vm = Create();
        await vm.RescanCommand.ExecuteAsync(null);
        vm.SelectedSkill = vm.VisibleSkills.First();

        _usage.SetException(new IOException("locked"));
        await Task.Delay(50);

        vm.Card!.UsageText.Should().Be("Usage unknown: Claude Code transcripts could not be read");
    }

    [Fact]
    public async Task Without_any_transcripts_the_unused_chip_stays_unavailable()
    {
        var vm = await WithUsage(UsageReport.Empty);

        vm.Chips.Single(c => c.Label == "unused").IsAvailable.Should().BeFalse();
        vm.UsageStatus.Should().Be("No Claude Code transcripts found");
    }

    [Fact]
    public async Task Status_reports_how_many_transcripts_were_read()
    {
        var vm = await WithUsage(Report());

        vm.UsageStatus.Should().StartWith("Usage from 4 transcripts, as of ");
    }
}

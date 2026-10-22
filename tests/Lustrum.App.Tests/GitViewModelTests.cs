using FluentAssertions;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using Lustrum.Core.Git;
using Lustrum.Core.Models;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.App.Tests;

public class GitViewModelTests
{
    private static readonly SkillRepository SkillsRepo = new("/home/u/.claude/skills", RepositoryRole.SkillsOnly);
    private static readonly SkillRepository AppRepo = new("/dev/app", RepositoryRole.Containing);
    private static readonly GitTracking Level = new("main", "origin/main", 0, 0);
    private static readonly GitCommit Commit = new("a1b2c3d4e5", "a1b2c3d", DateTimeOffset.Parse("2026-10-03T10:00:00Z"), "Dana", "Sharpen the prompt");

    private readonly FakeSkillGit _git = new();
    private readonly FakeSettings _settings = new(LustrumSettings.Default);
    private int _scans;

    public GitViewModelTests()
    {
        Report(TestData.Global, "design", new SkillLocation(SkillsRepo, "design"));
        Report(TestData.Project, "deploy", new SkillLocation(AppRepo, ".claude/skills/deploy"));
    }

    private void Report(
        SkillSource source, string name, SkillLocation location, GitTracking? tracking = null, IReadOnlyList<GitChange>? changes = null, SkillIncoming? incoming = null)
    {
        var state = tracking ?? Level;
        _git.Reports[$"{source.Id}#{name}"] = new SkillGitReport(
            location, state, changes ?? [], Commit, UpdatePolicy.Evaluate(location.Repository, state, changes ?? []), incoming);
    }

    private async Task<MainViewModel> Scanned()
    {
        var vm = new MainViewModel(
            (_, _) =>
            {
                _scans++;
                return Task.FromResult(TestData.Default());
            },
            _ => Task.FromResult(UsageReport.Empty),
            new AppServices(new FakeShell(), new FakeClipboard(), _settings, new FakeTheme(), _git, new FakeSkillWriter()));
        await vm.RescanCommand.ExecuteAsync(null);
        return vm;
    }

    private static void Select(MainViewModel vm, string name) => vm.SelectedSkill = vm.VisibleSkills.Single(s => s.Name == name);

    [Fact]
    public async Task A_skill_in_a_skills_repository_shows_branch_last_commit_and_actions()
    {
        var vm = await Scanned();

        Select(vm, "design");

        var git = vm.Card!.Git;
        git.IsVisible.Should().BeTrue();
        git.Headline.Should().Be("main → origin/main · up to date");
        git.LastCommit.Should().StartWith("Last commit a1b2c3d · Sharpen the prompt · Dana, ");
        git.RepositoryText.Should().Be("/home/u/.claude/skills · skills repository");
        git.ShowUpdate.Should().BeTrue();
        vm.CheckCardForUpdatesCommand.CanExecute(null).Should().BeTrue();
        vm.UpdateCardSkillCommand.CanExecute(null).Should().BeFalse();
        git.CheckNote.Should().Be("Counts are as of the last fetch; Check for updates fetches now");
    }

    [Fact]
    public async Task Plugin_skills_have_no_git_section_and_are_never_inspected()
    {
        var vm = await Scanned();

        Select(vm, "ui");

        vm.Card!.Git.IsVisible.Should().BeFalse();
        _git.Inspected.Should().NotContain(id => id.StartsWith(TestData.Plugin.Id));
    }

    [Fact]
    public async Task A_skill_outside_git_says_so_without_actions()
    {
        var vm = await Scanned();

        Select(vm, "broken-one");

        vm.Card!.Git.Headline.Should().Be("Not in a git repository");
        vm.Card.Git.ShowActions.Should().BeFalse();
        vm.CheckCardForUpdatesCommand.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task A_project_repository_gets_status_and_checks_but_no_update_button()
    {
        Report(TestData.Project, "deploy", new SkillLocation(AppRepo, ".claude/skills/deploy"),
            new GitTracking("main", "origin/main", 0, 2), [new GitChange(".claude/skills/deploy/SKILL.md", GitChangeKind.Modified)]);
        var vm = await Scanned();

        Select(vm, "deploy");

        var git = vm.Card!.Git;
        git.Headline.Should().Be("main → origin/main · 2 behind");
        git.ChangesTitle.Should().Be("1 uncommitted change");
        git.ChangeRows.Should().Equal(new GitChangeRow("M", ".claude/skills/deploy/SKILL.md"));
        git.ShowUpdate.Should().BeFalse();
        git.UpdateNote.Should().Be("This skill is part of the app repository; update it there with git");
        vm.CheckCardForUpdatesCommand.CanExecute(null).Should().BeTrue();
    }

    [Fact]
    public async Task Checking_a_card_fetches_and_enables_the_update()
    {
        _git.Checked[SkillsRepo.Root] = new GitTracking("main", "origin/main", 0, 3);
        var vm = await Scanned();
        Select(vm, "design");

        await vm.CheckCardForUpdatesCommand.ExecuteAsync(null);

        _git.Fetched.Should().Equal(SkillsRepo.Root);
        vm.Card!.Git.Headline.Should().Be("main → origin/main · 3 behind");
        vm.Card.Git.UpdateNote.Should().Be("3 new commits to pull");
        vm.Card.Git.CheckNote.Should().StartWith("Checked for updates at ");
        vm.UpdateCardSkillCommand.CanExecute(null).Should().BeTrue();
        vm.VisibleSkills.Single(s => s.Name == "design").Incoming.Should().Be(3);
    }

    [Fact]
    public async Task Updating_fast_forwards_then_rescans()
    {
        _git.Checked[SkillsRepo.Root] = new GitTracking("main", "origin/main", 0, 1);
        var vm = await Scanned();
        Select(vm, "design");
        await vm.CheckCardForUpdatesCommand.ExecuteAsync(null);

        await vm.UpdateCardSkillCommand.ExecuteAsync(null);

        _git.Updated.Should().Equal(SkillsRepo.Root);
        _scans.Should().Be(2);
        vm.Updates.StateOf(SkillsRepo).Should().BeOfType<CheckedRepo>().Which.Tracking.Behind.Should().Be(0);
        vm.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public async Task A_refused_update_is_reported_and_nothing_is_rescanned()
    {
        _git.Checked[SkillsRepo.Root] = new GitTracking("main", "origin/main", 0, 1);
        _git.UpdateFailure = new GitUpdateRefusedException("The repository has uncommitted changes");
        var vm = await Scanned();
        Select(vm, "design");
        await vm.CheckCardForUpdatesCommand.ExecuteAsync(null);

        await vm.UpdateCardSkillCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Be("Could not update skills: The repository has uncommitted changes");
        _scans.Should().Be(1);
        vm.Updates.StateOf(SkillsRepo).Should().BeOfType<CheckedRepo>();
        vm.Card!.Git.CanUpdate.Should().BeTrue();
    }

    [Fact]
    public async Task Checking_all_repositories_marks_only_skills_whose_own_folder_changed_upstream()
    {
        _git.Checked[SkillsRepo.Root] = new GitTracking("main", "origin/main", 0, 2);
        _git.Checked[AppRepo.Root] = new GitTracking("main", "origin/main", 0, 5);
        _git.IncomingByPath[".claude/skills/deploy"] = 0;
        var vm = await Scanned();
        var chip = vm.Chips.Single(c => c.Label == "updates");
        chip.IsAvailable.Should().BeFalse();

        await vm.CheckAllForUpdatesCommand.ExecuteAsync(null);
        chip.IsOn = true;

        _git.Fetched.Should().BeEquivalentTo(SkillsRepo.Root, AppRepo.Root);
        chip.IsAvailable.Should().BeTrue();
        vm.VisibleSkills.Select(s => s.Name).Should().Equal("design");
        vm.VisibleSkills.Single().UpdateHint.Should().Be("2 new commits upstream change this skill");
        _git.CountedPaths.Should().BeEquivalentTo("design", ".claude/skills/deploy");
        vm.UpdateStatus.Should().MatchRegex(@"^Checked 2 repositories at \d\d:\d\d · 1 skill has updates$");
        vm.IsCheckingUpdates.Should().BeFalse();
    }

    [Fact]
    public async Task A_skill_in_a_project_repository_counts_as_updated_when_upstream_commits_touch_it()
    {
        _git.Checked[AppRepo.Root] = new GitTracking("main", "origin/main", 0, 40);
        _git.IncomingByPath[".claude/skills/deploy"] = 1;
        var vm = await Scanned();

        await vm.CheckAllForUpdatesCommand.ExecuteAsync(null);

        vm.VisibleSkills.Single(s => s.Name == "deploy").Status.Should().EndWith("↓1");
    }

    [Fact]
    public async Task A_card_behind_its_upstream_lists_incoming_files_and_compares_with_the_upstream()
    {
        var incoming = new SkillIncoming(1, [new GitChange("design/SKILL.md", GitChangeKind.Modified)], "design/SKILL.md", "a\nb\n", "a\nc\n");
        Report(TestData.Global, "design", new SkillLocation(SkillsRepo, "design"), new GitTracking("main", "origin/main", 0, 3), incoming: incoming);
        var vm = await Scanned();

        Select(vm, "design");
        var card = vm.Card!;
        card.ShowIncomingCommand.Execute(null);
        await WaitUntil(() => card.Comparison.Summary.Length > 0);

        card.Git.IncomingTitle.Should().Be("1 new commit upstream changes this skill");
        card.Git.IncomingRows.Should().Equal(new GitChangeRow("M", "design/SKILL.md"));
        card.HasComparison.Should().BeTrue();
        card.SelectedTab.Should().Be(SkillCardViewModel.CompareTab);
        card.Comparison.Target!.Label.Should().Be("incoming from origin/main");
        card.Comparison.Summary.Should().Be("The update removes 1 line and adds 1 line in SKILL.md");
        card.Comparison.Legend.Should().Be("− goes with the update   + comes with the update");
        vm.CliCommand.Should().NotStartWith("diff");
    }

    [Fact]
    public async Task When_nothing_is_incoming_any_more_the_compare_tab_gives_way_to_the_overview()
    {
        var incoming = new SkillIncoming(1, [new GitChange("design/SKILL.md", GitChangeKind.Modified)], "design/SKILL.md", "a\n", "b\n");
        Report(TestData.Global, "design", new SkillLocation(SkillsRepo, "design"), new GitTracking("main", "origin/main", 0, 1), incoming: incoming);
        var vm = await Scanned();
        Select(vm, "design");
        var card = vm.Card!;
        card.ShowIncomingCommand.Execute(null);
        await WaitUntil(() => card.Comparison.Summary.Length > 0);

        Report(TestData.Global, "design", new SkillLocation(SkillsRepo, "design"));
        await vm.CheckCardForUpdatesCommand.ExecuteAsync(null);
        await WaitUntil(() => !card.HasComparison);

        card.SelectedTab.Should().Be(0);
        card.Comparison.Rows.Should().BeEmpty();
        card.Comparison.Summary.Should().BeEmpty();
        card.Comparison.IsLoading.Should().BeFalse();
    }

    [Fact]
    public async Task When_counting_fails_a_skills_repository_shows_its_whole_behind_count_and_a_project_none()
    {
        _git.Checked[SkillsRepo.Root] = new GitTracking("main", "origin/main", 0, 2);
        _git.Checked[AppRepo.Root] = new GitTracking("main", "origin/main", 0, 9);
        _git.IncomingFailure = new GitException("git log failed");
        var vm = await Scanned();

        await vm.CheckAllForUpdatesCommand.ExecuteAsync(null);

        vm.Updates.StateOf(SkillsRepo).Should().BeOfType<CheckedRepo>();
        vm.VisibleSkills.Single(s => s.Name == "design").Incoming.Should().Be(2);
        vm.VisibleSkills.Single(s => s.Name == "deploy").Incoming.Should().Be(0);
    }

    [Fact]
    public async Task A_project_skill_untouched_by_the_new_commits_says_so()
    {
        var incoming = new SkillIncoming(0, [], ".claude/skills/deploy/SKILL.md", "x", "x");
        Report(TestData.Project, "deploy", new SkillLocation(AppRepo, ".claude/skills/deploy"), new GitTracking("main", "origin/main", 0, 5), incoming: incoming);
        var vm = await Scanned();

        Select(vm, "deploy");

        vm.Card!.Git.IncomingTitle.Should().Be("None of the 5 new commits upstream change this skill");
        vm.Card.Git.CanShowIncoming.Should().BeFalse();
        vm.Card.HasComparison.Should().BeFalse();
    }

    [Fact]
    public async Task Failed_checks_are_counted_and_shown_on_the_card()
    {
        _git.CheckFailure = new GitException("git fetch failed: Could not resolve host");
        var vm = await Scanned();

        await vm.CheckAllForUpdatesCommand.ExecuteAsync(null);
        Select(vm, "design");

        vm.UpdateStatus.Should().EndWith("no skill has updates · 2 could not be checked");
        vm.Card!.Git.CheckNote.Should().Be("Check failed: git fetch failed: Could not resolve host");
        vm.Chips.Single(c => c.Label == "updates").IsAvailable.Should().BeFalse();
    }

    [Fact]
    public async Task Missing_git_is_explained_on_the_card()
    {
        _git.InspectFailure = new GitUnavailableException("No such file");
        var vm = await Scanned();

        Select(vm, "design");

        vm.Card!.Git.Headline.Should().Be("git is not installed or could not be started");
        vm.Card.Git.ShowActions.Should().BeFalse();
    }

    [Fact]
    public async Task Check_results_survive_a_rescan()
    {
        _git.Checked[SkillsRepo.Root] = new GitTracking("main", "origin/main", 0, 2);
        var vm = await Scanned();
        await vm.CheckAllForUpdatesCommand.ExecuteAsync(null);

        await vm.RescanCommand.ExecuteAsync(null);

        await WaitUntil(() => vm.VisibleSkills.Single(s => s.Name == "design").Incoming == 2);
    }

    [Fact]
    public async Task A_repository_being_updated_is_not_checked_again()
    {
        _git.Checked[SkillsRepo.Root] = new GitTracking("main", "origin/main", 0, 1);
        var vm = await Scanned();
        Select(vm, "design");
        await vm.CheckCardForUpdatesCommand.ExecuteAsync(null);
        var pending = new TaskCompletionSource<GitTracking>();
        _git.PendingUpdate = pending;

        var update = vm.UpdateCardSkillCommand.ExecuteAsync(null);
        vm.CheckAllForUpdatesCommand.CanExecute(null).Should().BeFalse();
        vm.CheckCardForUpdatesCommand.CanExecute(null).Should().BeFalse();
        pending.SetResult(new GitTracking("main", "origin/main", 0, 0));
        await update;

        _git.Fetched.Should().Equal(SkillsRepo.Root);
        vm.CheckAllForUpdatesCommand.CanExecute(null).Should().BeTrue();
    }

    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (!condition())
        {
            DateTime.UtcNow.Should().BeBefore(deadline, "the condition should become true");
            await Task.Delay(20);
        }
    }
}

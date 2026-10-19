using FluentAssertions;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using Lustrum.Core.Indexing;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.App.Tests;

public class FeatureViewModelTests
{
    private readonly FakeShell _shell = new();
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTheme _theme = new();
    private FakeSettings _settings = new(LustrumSettings.Default);
    private readonly List<ScanOptions> _scanOptions = [];

    private MainViewModel Create(ScanResult result)
    {
        return new MainViewModel(
            (options, _) =>
            {
                _scanOptions.Add(options);
                return Task.FromResult(result);
            },
            _ => Task.FromResult(UsageReport.Empty),
            new AppServices(_shell, _clipboard, _settings, _theme, new FakeSkillGit(), new FakeSkillWriter()));
    }

    private async Task<MainViewModel> Scanned(ScanResult? result = null)
    {
        var vm = Create(result ?? TestData.WithShadowing());
        await vm.RescanCommand.ExecuteAsync(null);
        return vm;
    }

    private static ProjectViewOption View(MainViewModel vm, ProjectView view) =>
        vm.ProjectViews.Single(v => v.View == view);

    private static NavNode Node(MainViewModel vm, string title) =>
        Flatten(vm.NavRoots).First(n => n.Title == title);

    private static IEnumerable<NavNode> Flatten(IEnumerable<NavNode> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));

    [Fact]
    public async Task Effective_set_is_available_only_for_a_project_node()
    {
        var vm = await Scanned();

        vm.SelectedNode = Node(vm, "Global");
        vm.CanChooseProjectView.Should().BeFalse();

        vm.SelectedNode = Node(vm, "app");
        vm.CanChooseProjectView.Should().BeTrue();
    }

    [Fact]
    public async Task Effective_set_lists_what_the_project_loads_and_marks_shadowed_skills()
    {
        var vm = await Scanned();
        vm.SelectedNode = Node(vm, "app");

        vm.SelectedProjectView = View(vm, ProjectView.Effective);

        vm.ListTitle.Should().Be("app · effective set");
        vm.VisibleSkills.Select(s => s.Skill.FullName).Should().BeEquivalentTo("design", "design", "notes", "unity:ui");
        var shadowed = vm.VisibleSkills.Single(s => s.IsShadowed);
        shadowed.Skill.Source.Kind.Should().Be(SourceKind.Project);
        shadowed.AvailabilityLabel.Should().Be("shadowed by Global");
        vm.VisibleSkills.Single(s => s.Skill.FullName == "design" && s.Skill.Source.Kind == SourceKind.Global).AvailabilityLabel.Should().Be("overrides app");
    }

    [Fact]
    public async Task Effective_set_card_explains_the_shadowing()
    {
        var vm = await Scanned();
        vm.SelectedNode = Node(vm, "app");
        vm.SelectedProjectView = View(vm, ProjectView.Effective);

        vm.SelectedSkill = vm.VisibleSkills.Single(s => s.IsShadowed);

        vm.Card!.AvailabilityNote.Should().Be("Not loaded in app: the copy in Global takes precedence");
    }

    [Fact]
    public async Task Effective_set_card_names_what_the_winner_beats()
    {
        var vm = await Scanned();
        vm.SelectedNode = Node(vm, "app");
        vm.SelectedProjectView = View(vm, ProjectView.Effective);

        vm.SelectedSkill = vm.VisibleSkills.Single(s => s.Skill.FullName == "design" && !s.IsShadowed);

        vm.Card!.AvailabilityNote.Should().Be("Loaded in app, ahead of the project's own copy");
    }

    [Fact]
    public async Task Effective_flag_is_ignored_outside_projects()
    {
        var vm = await Scanned();
        vm.SelectedNode = Node(vm, "app");
        vm.SelectedProjectView = View(vm, ProjectView.Effective);

        vm.SelectedNode = Node(vm, "Global");

        vm.VisibleSkills.Should().OnlyContain(s => s.Skill.Source.Kind == SourceKind.Global);
        vm.VisibleSkills.Should().OnlyContain(s => !s.IsShadowed);
    }

    [Fact]
    public async Task Project_view_starts_on_own_skills()
    {
        var vm = await Scanned();

        vm.SelectedNode = Node(vm, "app");

        vm.SelectedProjectView!.View.Should().Be(ProjectView.Own);
        vm.ListTitle.Should().Be("app");
        vm.VisibleSkills.Should().OnlyContain(s => s.Skill.Source.Kind == SourceKind.Project);
    }

    [Fact]
    public async Task Missing_view_lists_what_the_project_does_not_load()
    {
        var vm = await Scanned();
        vm.SelectedNode = Node(vm, "app");

        vm.SelectedProjectView = View(vm, ProjectView.Missing);

        vm.ListTitle.Should().Be("app · missing");
        var item = vm.VisibleSkills.Should().ContainSingle().Subject;
        item.Skill.Name.Should().Be("elsewhere");
        item.AvailabilityLabel.Should().BeNull();
    }

    [Fact]
    public async Task Missing_view_card_explains_why()
    {
        var vm = await Scanned();
        vm.SelectedNode = Node(vm, "app");
        vm.SelectedProjectView = View(vm, ProjectView.Missing);

        vm.SelectedSkill = vm.VisibleSkills.Single();

        vm.Card!.AvailabilityNote.Should().Be("Not loaded in app: it is stored in empty");
    }

    [Fact]
    public async Task A_cleared_view_selection_falls_back_to_own_instead_of_crashing()
    {
        var vm = await Scanned();
        vm.SelectedNode = Node(vm, "app");

        vm.SelectedProjectView = null;
        var typing = () => vm.SearchText = "design";

        typing.Should().NotThrow();
        vm.ListTitle.Should().Be("app");
    }

    [Fact]
    public async Task Chosen_view_is_kept_when_moving_to_another_project()
    {
        var vm = await Scanned();
        vm.ShowEmptySources = true;
        vm.SelectedNode = Node(vm, "app");
        vm.SelectedProjectView = View(vm, ProjectView.Missing);

        vm.SelectedNode = Node(vm, "empty");

        vm.ListTitle.Should().Be("empty · missing");
    }

    [Fact]
    public async Task Chips_offer_own_prefixes_and_fixed_tags()
    {
        var vm = await Scanned();

        vm.Chips.Select(c => c.Label).Should().Equal("sb-*", "plugin", "synced", "command", "broken", "duplicate", "unused", "updates");
    }

    [Fact]
    public async Task Turning_on_a_chip_filters_by_tag()
    {
        var tagged = TestData.Scan(
            [TestData.Global, TestData.Plugin],
            [
                TestData.Skill(TestData.Global, "a") with { Tags = ["sb-"] },
                TestData.Skill(TestData.Plugin, "ui") with { Tags = ["plugin"] },
            ]);
        var vm = await Scanned(tagged);

        vm.Chips.Single(c => c.Label == "plugin").IsOn = true;

        vm.VisibleSkills.Select(s => s.Name).Should().Equal("ui");
    }

    [Fact]
    public async Task Applying_settings_saves_them_applies_theme_and_rescans_with_new_options()
    {
        var vm = await Scanned();
        vm.SettingsEditor.OwnPrefixesText = "me-, team-";
        vm.SettingsEditor.ExtraRootsText = "/work\n/play";
        vm.SettingsEditor.SelectedTheme = ThemePreference.Dark;

        await vm.ApplySettingsCommand.ExecuteAsync(null);

        _settings.Saved.Should().ContainSingle();
        _settings.Saved[0].OwnPrefixes.Should().Equal("me-", "team-");
        _settings.Saved[0].ExtraRoots.Should().Equal("/work", "/play");
        _theme.Applied.Last().Should().Be(ThemePreference.Dark);
        _scanOptions.Last().OwnPrefixes.Should().Equal("me-", "team-");
        vm.Chips.Select(c => c.Label).Should().StartWith(new[] { "me-*", "team-*" });
    }

    [Fact]
    public async Task Failed_settings_save_is_reported()
    {
        _settings = new FakeSettings(LustrumSettings.Default) { FailSave = true };
        var vm = await Scanned();

        await vm.ApplySettingsCommand.ExecuteAsync(null);

        vm.ErrorMessage.Should().Contain("disk full");
    }

    [Fact]
    public void Theme_and_settings_warning_are_applied_at_startup()
    {
        _settings = new FakeSettings(LustrumSettings.Default with { Theme = ThemePreference.Light }, "bad json");

        var vm = Create(TestData.Default());

        _theme.Applied.Should().Equal(ThemePreference.Light);
        vm.ErrorMessage.Should().Be("bad json");
    }

    [Fact]
    public async Task Problems_node_switches_to_the_problems_list()
    {
        var vm = await Scanned(TestData.Default());

        vm.SelectedNode = Node(vm, "Problems");

        vm.IsProblemsView.Should().BeTrue();
        vm.VisibleProblems.Select(p => p.Subject).Should().Equal("broken-one", "gone");
        vm.VisibleProblems[0].Message.Should().Be("Missing name");
        vm.VisibleProblems[1].Message.Should().Be("Project folder no longer exists");
    }

    [Fact]
    public async Task Selecting_a_skill_problem_opens_its_card()
    {
        var vm = await Scanned(TestData.Default());
        vm.SelectedNode = Node(vm, "Problems");

        vm.SelectedProblem = vm.VisibleProblems[0];

        vm.Card!.Title.Should().Be("broken-one");
    }

    [Fact]
    public async Task Default_sort_is_best_match()
    {
        var vm = await Scanned(TestData.Scan(
            [TestData.Global],
            [TestData.Skill(TestData.Global, "aaa-notes"), TestData.Skill(TestData.Global, "notes")]));

        vm.SelectedSort.Mode.Should().Be(SortMode.Relevance);
        vm.SearchText = "notes";

        vm.VisibleSkills.Select(s => s.Name).Should().Equal("notes", "aaa-notes");
    }

    [Fact]
    public async Task Content_search_adds_skills_whose_body_matches()
    {
        var file = Path.Combine(Path.GetTempPath(), $"lustrum-body-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(file, "---\nname: hidden\ndescription: d\n---\nthe needle is here\n");
        try
        {
            var skill = TestData.Skill(TestData.Global, "hidden") with { Path = file };
            var vm = await Scanned(TestData.Scan([TestData.Global], [skill]));
            vm.SearchText = "needle";
            vm.VisibleSkills.Should().BeEmpty();

            vm.SearchInContent = true;

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (vm.VisibleSkills.Count == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            vm.VisibleSkills.Select(s => s.Name).Should().Equal("hidden");
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Content_search_is_refreshed_after_a_rescan()
    {
        var file = Path.Combine(Path.GetTempPath(), $"lustrum-body-{Guid.NewGuid():N}.md");
        await File.WriteAllTextAsync(file, "---\nname: later\ndescription: d\n---\nnothing yet\n");
        try
        {
            var skill = TestData.Skill(TestData.Global, "later") with { Path = file };
            var vm = await Scanned(TestData.Scan([TestData.Global], [skill]));
            vm.SearchInContent = true;
            vm.SearchText = "needle";
            await Task.Delay(600);
            vm.VisibleSkills.Should().BeEmpty();

            await File.WriteAllTextAsync(file, "---\nname: later\ndescription: d\n---\nnow the needle\n");
            await vm.RescanCommand.ExecuteAsync(null);

            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (vm.VisibleSkills.Count == 0 && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50);
            }

            vm.VisibleSkills.Select(s => s.Name).Should().Equal("later");
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Fact]
    public async Task Open_in_editor_uses_the_configured_command()
    {
        _settings = new FakeSettings(LustrumSettings.Default with { EditorCommand = "code" });
        var vm = await Scanned();
        vm.SelectedSkill = vm.VisibleSkills.First();

        vm.OpenInEditorCommand.Execute(null);

        _shell.Edited.Should().ContainSingle().Which.Command.Should().Be("code");
    }

    [Fact]
    public async Task Scan_applied_event_carries_the_result()
    {
        var vm = Create(TestData.Default());
        ScanResult? seen = null;
        vm.ScanApplied += r => seen = r;

        await vm.RescanCommand.ExecuteAsync(null);

        seen.Should().NotBeNull();
    }
}

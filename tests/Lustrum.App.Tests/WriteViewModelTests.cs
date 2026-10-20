using System.Collections.Immutable;
using FluentAssertions;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Models;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.App.Tests;

public class WriteViewModelTests
{
    private const string UserSettings = "/home/u/.claude/settings.json";
    private static readonly string AppLocal = Path.Combine(TestData.Project.RootPath, "settings.local.json");

    private readonly FakeSkillWriter _writer = new();
    private SettingsLayers _overrides = SettingsLayers.Empty with { User = SettingsLayer.Empty(SettingsScope.User, UserSettings) };
    private int _scans;

    private static ImmutableDictionary<string, SkillVisibility> Map(params (string Name, SkillVisibility Value)[] entries) =>
        entries.ToImmutableDictionary(e => e.Name, e => e.Value, StringComparer.Ordinal);

    private async Task<MainViewModel> Scanned(ScanResult? scan = null)
    {
        var vm = new MainViewModel(
            (_, _) =>
            {
                _scans++;
                return Task.FromResult((scan ?? TestData.WithShadowing()) with { Settings = _overrides });
            },
            _ => Task.FromResult(UsageReport.Empty),
            new AppServices(new FakeShell(), new FakeClipboard(), new FakeSettings(LustrumSettings.Default), new FakeTheme(), new FakeSkillGit(), _writer));
        await vm.RescanCommand.ExecuteAsync(null);
        return vm;
    }

    private static void Select(MainViewModel vm, string name, SourceKind kind) =>
        vm.SelectedSkill = vm.VisibleSkills.Single(s => s.Name == name && s.Skill.Source.Kind == kind);

    private void ProjectLayers(SkillSource project, ImmutableDictionary<string, SkillVisibility>? shared = null, ImmutableDictionary<string, SkillVisibility>? local = null)
    {
        var settings = ProjectSettings.Empty(project);
        _overrides = _overrides with
        {
            Projects = _overrides.Projects.SetItem(project.Id, new ProjectSettings(
                settings.Shared with { Skills = shared ?? settings.Shared.Skills },
                settings.Local with { Skills = local ?? settings.Local.Skills })),
        };
    }

    [Fact]
    public async Task A_personal_skill_writes_its_visibility_to_user_settings_then_rescans()
    {
        var vm = await Scanned();
        Select(vm, "notes", SourceKind.Global);
        vm.Card!.SelectedVisibility!.Value.Should().Be(SkillVisibility.On);
        vm.Card.VisibilityHint.Should().StartWith("Saved in your user settings for every project");

        vm.Card.SelectedVisibility = VisibilityOption.For(SkillVisibility.Off);
        await Task.Yield();

        _writer.Written.Should().Equal((UserSettings, "notes", (SkillVisibility?)SkillVisibility.Off));
        _scans.Should().Be(2);
        vm.ActionNotice.Should().Be("notes is now Off in your user settings");
    }

    [Fact]
    public async Task A_project_skill_writes_to_that_projects_local_settings()
    {
        var vm = await Scanned();
        Select(vm, "design", SourceKind.Project);

        vm.Card!.SelectedVisibility = VisibilityOption.For(SkillVisibility.NameOnly);
        await Task.Yield();

        _writer.Written.Should().Equal((AppLocal, "design", (SkillVisibility?)SkillVisibility.NameOnly));
        vm.Card!.VisibilityHint.Should().StartWith("Saved in app/.claude/settings.local.json, for you only");
    }

    [Fact]
    public async Task A_project_skill_hidden_by_shared_settings_shows_off_and_turning_it_on_writes_an_explicit_on()
    {
        ProjectLayers(TestData.Project, shared: Map(("design", SkillVisibility.Off)));
        var vm = await Scanned();
        Select(vm, "design", SourceKind.Project);
        vm.Card!.SelectedVisibility!.Value.Should().Be(SkillVisibility.Off);
        vm.Card.VisibilityNote.Should().Be("Currently Off because of .claude/settings.json");

        vm.Card.SelectedVisibility = VisibilityOption.For(SkillVisibility.On);
        await Task.Yield();

        _writer.Written.Should().Equal((AppLocal, "design", (SkillVisibility?)SkillVisibility.On));
    }

    [Fact]
    public async Task A_second_pick_while_saving_is_ignored()
    {
        var vm = await Scanned();
        Select(vm, "notes", SourceKind.Global);
        var pending = new TaskCompletionSource();
        _writer.Pending = pending;
        var card = vm.Card!;

        card.SelectedVisibility = VisibilityOption.For(SkillVisibility.Off);
        card.CanEditVisibility.Should().BeFalse();
        card.SelectedVisibility = VisibilityOption.For(SkillVisibility.NameOnly);
        pending.SetResult();
        await Task.Yield();

        _writer.Written.Should().ContainSingle().Which.Visibility.Should().Be(SkillVisibility.Off);
    }

    [Fact]
    public async Task Showing_a_card_never_writes()
    {
        _overrides = _overrides with { User = _overrides.User with { Skills = Map(("notes", SkillVisibility.Off)) } };
        var vm = await Scanned();

        Select(vm, "notes", SourceKind.Global);

        vm.Card!.SelectedVisibility!.Value.Should().Be(SkillVisibility.Off);
        _writer.Written.Should().BeEmpty();
    }

    [Fact]
    public async Task Plugin_skills_have_no_visibility_control()
    {
        var vm = await Scanned();

        Select(vm, "ui", SourceKind.Plugin);

        vm.Card!.CanSetVisibility.Should().BeFalse();
    }

    [Fact]
    public async Task A_failed_write_is_reported_and_the_selection_goes_back()
    {
        _writer.Failure = new SettingsWriteException("settings.json is not plain JSON, so it was left alone");
        var vm = await Scanned();
        Select(vm, "notes", SourceKind.Global);

        vm.Card!.SelectedVisibility = VisibilityOption.For(SkillVisibility.Off);
        await Task.Yield();

        vm.ErrorMessage.Should().Be("Could not change notes: settings.json is not plain JSON, so it was left alone");
        vm.Card.SelectedVisibility!.Value.Should().Be(SkillVisibility.On);
        _scans.Should().Be(1);
    }

    [Fact]
    public async Task Rows_show_turned_off_skills_dimmed_with_their_state()
    {
        _overrides = _overrides with { User = _overrides.User with { Skills = Map(("notes", SkillVisibility.Off), ("design", SkillVisibility.NameOnly)) } };
        var vm = await Scanned();

        var notes = vm.VisibleSkills.Single(s => s.Name == "notes");
        notes.AvailabilityLabel.Should().Be("off");
        notes.RowOpacity.Should().Be(0.5);
        vm.VisibleSkills.Single(s => s.Name == "design" && s.Skill.Source.Kind == SourceKind.Global).AvailabilityLabel.Should().Be("name only");
        vm.VisibleSkills.Single(s => s.Name == "design" && s.Skill.Source.Kind == SourceKind.Project).AvailabilityLabel.Should().Be("name only", "user settings cover every skill with that name");
    }

    [Fact]
    public async Task In_a_project_the_card_says_when_project_settings_take_precedence()
    {
        ProjectLayers(TestData.Project, shared: Map(("notes", SkillVisibility.Off)));
        var vm = await Scanned();
        vm.SelectedNode = vm.NavRoots.SelectMany(n => n.Children).Single(n => n.Title == "app");
        vm.SelectedProjectView = vm.ProjectViews.Single(v => v.View == ProjectView.Effective);

        Select(vm, "notes", SourceKind.Global);

        vm.VisibleSkills.Single(s => s.Name == "notes").AvailabilityLabel.Should().Be("off");
        vm.Card!.VisibilityNote.Should().Be("In app it is Off, set in .claude/settings.json, which takes precedence");
        vm.Card.AvailabilityNote.Should().Be("Turned off in app: hidden from Claude and from the / menu");
    }

    [Fact]
    public async Task Copy_targets_list_personal_skills_and_projects_with_refusals_disabled()
    {
        var vm = await Scanned();
        Select(vm, "notes", SourceKind.Global);

        var targets = vm.Card!.CopyTargets;

        targets.Select(t => t.Label).Should().Equal("app", "empty");
        targets.Should().OnlyContain(t => t.Copy.CanExecute(null));

        Select(vm, "design", SourceKind.Global);
        var app = vm.Card!.CopyTargets.Single(t => t.Label.StartsWith("app"));
        app.Label.Should().Be("app (app already has a skill named design)");
        app.Copy.CanExecute(null).Should().BeFalse();
    }

    [Fact]
    public async Task Copying_writes_through_the_writer_and_rescans()
    {
        var vm = await Scanned();
        Select(vm, "notes", SourceKind.Global);

        await vm.Card!.CopyTargets.Single(t => t.Label == "empty").Copy.ExecuteAsync(null);

        _writer.Copied.Should().ContainSingle().Which.To.Should().Be(Path.Combine(TestData.EmptyProject.RootPath, "skills", "notes"));
        vm.ActionNotice.Should().Be("Copied notes to empty");
        _scans.Should().Be(2);
    }
}

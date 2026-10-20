using FluentAssertions;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using System.Collections.Immutable;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Models;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.App.Tests;

public class ParityViewModelTests
{
    private static readonly SkillSource Managed = new("managed", SourceKind.Managed, "Managed", "/etc/claude-code/.claude", null, true);
    private static readonly SkillSource Nested = new("project:/dev/app/apps/api", SourceKind.Project, "app/apps/api", "/dev/app/apps/api/.claude", null, true);

    private readonly FakeSkillWriter _writer = new();

    private async Task<MainViewModel> Scanned(ScanResult scan)
    {
        var vm = new MainViewModel(
            (_, _) => Task.FromResult(scan),
            _ => Task.FromResult(UsageReport.Empty),
            new AppServices(new FakeShell(), new FakeClipboard(), new FakeSettings(LustrumSettings.Default), new FakeTheme(), new FakeSkillGit(), _writer));
        await vm.RescanCommand.ExecuteAsync(null);
        return vm;
    }

    [Fact]
    public async Task Managed_skills_get_their_own_section_key_and_group()
    {
        var vm = await Scanned(TestData.Scan([Managed, TestData.Global], [TestData.Skill(Managed, "policy"), TestData.Skill(TestData.Global, "notes")]));

        vm.NavSections.Select(s => s.Key).Should().StartWith(["A", "M", "G"]);
        vm.ListRows.OfType<ListHeader>().First().Title.Should().Be("MANAGED");
        vm.SelectSection("M").Should().BeTrue();
        vm.CliCommand.Should().Be("list --source managed");
        vm.VisibleSkills.Single().SourceLabel.Should().Be("Managed");
    }

    [Fact]
    public async Task Skills_below_the_project_show_as_on_demand_with_their_folder()
    {
        var scan = TestData.Scan([TestData.Global, TestData.Project, Nested], [TestData.Skill(TestData.Project, "deploy"), TestData.Skill(Nested, "deploy")]);
        var vm = await Scanned(scan);
        vm.SelectNodeCommand.Execute(vm.NavSections.SelectMany(s => s.Children).Select(c => c.Node).First(n => n.Title == "app"));
        vm.SelectedProjectView = vm.ProjectViews.Single(v => v.View == ProjectView.Effective);

        var nested = vm.VisibleSkills.Single(s => s.Skill.Source.Id == Nested.Id);
        nested.AvailabilityLabel.Should().Be("on demand · apps/api");
        nested.IsShadowed.Should().BeFalse();
        vm.SelectedSkill = nested;
        vm.Card!.AvailabilityNote.Should().Be("Loads in app once Claude works on files under apps/api");
    }

    private static NavNode AppNode(MainViewModel vm)
    {
        vm.ShowEmptySources = true;
        return vm.NavSections.SelectMany(s => s.Children).Select(c => c.Node).First(n => n.Title == "app");
    }

    [Fact]
    public async Task A_plugin_turned_on_only_by_the_project_is_shown_as_loaded_there()
    {
        var plugin = TestData.Plugin with { IsEnabled = false };
        var empty = ProjectSettings.Empty(TestData.Project);
        var settings = SettingsLayers.Empty with
        {
            Projects = ImmutableDictionary<string, ProjectSettings>.Empty.Add(TestData.Project.Id,
                empty with { Local = empty.Local with { Plugins = ImmutableDictionary<string, bool>.Empty.Add("unity@market", true) } }),
        };
        var vm = await Scanned(TestData.Scan([TestData.Global, TestData.Project, plugin], [TestData.Skill(plugin, "ui")]) with { Settings = settings });
        vm.VisibleSkills.Single().AvailabilityLabel.Should().Be("disabled");

        vm.SelectNodeCommand.Execute(AppNode(vm));
        vm.SelectedProjectView = vm.ProjectViews.Single(v => v.View == ProjectView.Effective);

        var row = vm.VisibleSkills.Single();
        row.AvailabilityLabel.Should().BeNull();
        row.IsDisabled.Should().BeFalse();
    }

    [Fact]
    public async Task Visibility_of_a_skill_from_a_nested_folder_is_saved_in_the_session_project()
    {
        var vm = await Scanned(TestData.Scan([TestData.Global, TestData.Project, Nested], [TestData.Skill(Nested, "reports")]));
        vm.SelectNodeCommand.Execute(AppNode(vm));
        vm.SelectedProjectView = vm.ProjectViews.Single(v => v.View == ProjectView.Effective);
        vm.SelectedSkill = vm.VisibleSkills.Single();

        vm.Card!.VisibilityHint.Should().StartWith("Saved in app/.claude/settings.local.json");
        vm.Card.SelectedVisibility = VisibilityOption.For(SkillVisibility.Off);
        await Task.Yield();

        _writer.Written.Should().ContainSingle().Which.Path.Should().Be(Path.Combine(TestData.Project.RootPath, "settings.local.json"));
    }
}

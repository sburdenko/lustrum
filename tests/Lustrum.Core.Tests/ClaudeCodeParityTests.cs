using FluentAssertions;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class ClaudeCodeParityTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();

    public void Dispose() => _home.Dispose();

    private static string Doc(string name) => $"---\nname: {name}\ndescription: d\n---\nbody\n";

    private string Project(string relative)
    {
        var path = _home.Project(relative);
        _home.RegisterProjectFolder(path);
        return path;
    }

    private string Skill(string folder, string name) =>
        _home.File(Path.Combine(folder, ".claude", "skills", name, "SKILL.md"), Doc(name));

    private async Task<(ScanResult Scan, SkillSource Project)> Scan(string project)
    {
        var scan = await SkillScanner.ScanAsync(_home.Paths);
        return (scan, scan.Sources.Single(s => s.Id == "project:" + project));
    }

    [Fact]
    public async Task Parent_folders_up_to_the_repository_root_load_with_the_project()
    {
        var repo = _home.Dir("dev/mono");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        Skill(repo, "lint");
        Skill(Path.Combine(repo, "packages"), "pack");
        var web = Project("dev/mono/packages/web");
        Skill(web, "web-only");

        var (scan, project) = await Scan(web);
        var set = EffectiveSetBuilder.Build(scan, project);

        set.Entries.Where(e => e.Availability == Availability.Active).Select(e => e.Skill.Name)
            .Should().BeEquivalentTo("lint", "pack", "web-only");
        scan.Sources.Should().Contain(s => s.Id == "project:" + repo);
    }

    [Fact]
    public async Task Without_a_repository_parent_folders_are_not_loaded()
    {
        var parent = _home.Dir("dev/plain");
        Skill(parent, "outer");
        var app = Project("dev/plain/app");
        Skill(app, "inner");

        var (scan, project) = await Scan(app);

        EffectiveSetBuilder.Build(scan, project).Entries.Select(e => e.Skill.Name).Should().Equal("inner");
    }

    [Fact]
    public async Task Skills_below_the_project_load_on_demand_and_never_compete_for_a_name()
    {
        var app = Project("dev/app");
        Skill(app, "deploy");
        Skill(Path.Combine(app, "apps", "api"), "deploy");
        Skill(Path.Combine(app, "node_modules", "pkg"), "hidden");
        Skill(Path.Combine(app, ".cache", "x"), "dotted");

        var (scan, project) = await Scan(app);
        var set = EffectiveSetBuilder.Build(scan, project);

        var nested = set.Entries.Single(e => e.Availability == Availability.OnDemand);
        nested.Skill.Source.DisplayName.Should().Be("app/apps/api");
        nested.LoadsUnder.Should().Be("apps/api");
        nested.Label(s => s.DisplayName).Should().Be("on demand · apps/api");
        set.Entries.Single(e => e.Skill.Source.Id == project.Id).Availability.Should().Be(Availability.Active);
        scan.Skills.Should().NotContain(s => s.Name == "hidden" || s.Name == "dotted");
    }

    [Fact]
    public async Task Managed_skills_win_over_personal_ones()
    {
        _home.File(Path.Combine(_home.ManagedDir, ".claude", "skills", "policy", "SKILL.md"), Doc("policy"));
        _home.GlobalSkill("policy", Doc("policy"));
        var app = Project("dev/app");

        var (scan, project) = await Scan(app);
        var set = EffectiveSetBuilder.Build(scan, project);

        scan.Sources.First().Kind.Should().Be(SourceKind.Managed);
        set.Entries.Single(e => e.Availability == Availability.Active).Skill.Source.Kind.Should().Be(SourceKind.Managed);
        set.Entries.Single(e => e.Availability == Availability.Shadowed).ShadowedBy!.Source.Kind.Should().Be(SourceKind.Managed);
    }

    [Fact]
    public async Task Managed_settings_merge_their_drop_ins_in_name_order_and_win_over_every_other_file()
    {
        _home.File(Path.Combine(_home.ManagedDir, "managed-settings.json"), """{ "skillOverrides": { "a": "off", "b": "off" } }""");
        _home.File(Path.Combine(_home.ManagedDir, "managed-settings.d", "20-late.json"), """{ "skillOverrides": { "a": "name-only" } }""");
        _home.File(Path.Combine(_home.ManagedDir, "managed-settings.d", "10-early.json"), """{ "skillOverrides": { "a": "on" } }""");
        _home.File(Path.Combine(_home.ManagedDir, "managed-settings.d", ".hidden.json"), """{ "skillOverrides": { "a": "off" } }""");
        var app = Project("dev/app");
        _home.File(Path.Combine(app, ".claude", "settings.local.json"), """{ "skillOverrides": { "b": "on" } }""");

        var (scan, project) = await Scan(app);

        scan.Settings.Resolve("a", project).Value.Should().Be(SkillVisibility.NameOnly);
        scan.Settings.Resolve("b", project).Should().Match<ResolvedVisibility>(r => r.Value == SkillVisibility.Off && r.From!.Scope == SettingsScope.Managed);
    }

    [Fact]
    public async Task A_project_can_turn_a_plugin_off_or_on_for_itself()
    {
        var kit = _home.InstallPlugin("m", "kit", "1");
        _home.File(Path.Combine(kit, "skills", "k", "SKILL.md"), Doc("k"));
        var quiet = Project("dev/quiet");
        var loud = Project("dev/loud");
        _home.File(Path.Combine(quiet, ".claude", "settings.json"), """{ "enabledPlugins": { "kit@m": false } }""");

        var scan = await SkillScanner.ScanAsync(_home.Paths);
        SkillSource ProjectAt(string path) => scan.Sources.Single(s => s.Id == "project:" + path);

        EffectiveSetBuilder.Build(scan, ProjectAt(quiet)).Entries.Should().BeEmpty();
        EffectiveSetBuilder.Build(scan, ProjectAt(loud)).Entries.Select(e => e.Skill.FullName).Should().Equal("kit:k");
        MissingSetBuilder.Build(scan, ProjectAt(quiet)).Entries.Select(e => e.Skill.FullName).Should().Equal("kit:k");
        scan.Sources.Single(s => s.Kind == SourceKind.Plugin).IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Local_settings_beat_user_settings_for_plugins_and_managed_settings_beat_both()
    {
        var kit = _home.InstallPlugin("m", "kit", "1");
        _home.File(Path.Combine(kit, "skills", "k", "SKILL.md"), Doc("k"));
        _home.EnablePlugins(("kit@m", false));
        var app = Project("dev/app");
        _home.File(Path.Combine(app, ".claude", "settings.local.json"), """{ "enabledPlugins": { "kit@m": true } }""");

        var (scan, project) = await Scan(app);

        scan.Sources.Single(s => s.Kind == SourceKind.Plugin).IsEnabled.Should().BeFalse("user settings turn it off outside the project");
        EffectiveSetBuilder.Build(scan, project).Entries.Should().ContainSingle();

        _home.File(Path.Combine(_home.ManagedDir, "managed-settings.json"), """{ "enabledPlugins": { "kit@m": false } }""");
        (scan, project) = await Scan(app);
        EffectiveSetBuilder.Build(scan, project).Entries.Should().BeEmpty();
    }

    [Fact]
    public void Plugin_keys_come_from_the_source_id()
    {
        var plugin = new SkillSource("plugin:acme/kit", SourceKind.Plugin, "kit (acme)", "/p", null, true, Namespace: "kit");

        SettingsLayers.PluginKey(plugin).Should().Be("kit@acme");
        SettingsLayers.PluginKey(plugin with { Kind = SourceKind.Synced }).Should().BeNull();
    }

    [Fact]
    public async Task Folders_above_the_repository_root_do_not_load()
    {
        var outside = _home.Dir("dev");
        Skill(outside, "outside");
        var repo = _home.Dir("dev/mono");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        var web = Project("dev/mono/web");
        Skill(web, "web");

        var (scan, project) = await Scan(web);

        EffectiveSetBuilder.Build(scan, project).Entries.Select(e => e.Skill.Name).Should().Equal("web");
    }

    [Fact]
    public async Task Worktrees_and_nested_clones_are_projects_of_their_own_not_on_demand_folders()
    {
        var repo = Project("dev/repo");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        Skill(repo, "main");
        var worktree = Project("dev/repo/.claude/worktrees/feature");
        _home.File(Path.Combine(worktree, ".git"), "gitdir: elsewhere");
        Skill(worktree, "main");
        var clone = Project("dev/repo/vendor/lib");
        Directory.CreateDirectory(Path.Combine(clone, ".git"));
        Skill(clone, "lib");

        var (scan, project) = await Scan(repo);

        EffectiveSetBuilder.Build(scan, project).Entries.Should().ContainSingle().Which.Skill.Source.Id.Should().Be(project.Id);
    }

    [Fact]
    public async Task The_same_folder_written_with_a_trailing_separator_is_one_source()
    {
        var app = _home.Project("dev/app");
        Skill(app, "deploy");
        _home.RegisterInClaudeJson(app + Path.DirectorySeparatorChar);
        _home.RegisterProjectFolder(app);

        var scan = await SkillScanner.ScanAsync(_home.Paths);

        scan.Sources.Count(s => s.Kind == SourceKind.Project).Should().Be(1);
        scan.Skills.Should().ContainSingle(s => s.Name == "deploy");
    }

    [Fact]
    public async Task A_broken_managed_drop_in_is_reported_and_the_others_still_apply()
    {
        _home.File(Path.Combine(_home.ManagedDir, "managed-settings.d", "10-bad.json"), "{ not json");
        _home.File(Path.Combine(_home.ManagedDir, "managed-settings.d", "20-good.json"), """{ "skillOverrides": { "a": "off" } }""");

        var scan = await SkillScanner.ScanAsync(_home.Paths);

        scan.Settings.Resolve("a", null).Value.Should().Be(SkillVisibility.Off);
        scan.Issues.Should().ContainSingle(i => i.Path.EndsWith("10-bad.json"));
    }

    [Fact]
    public async Task Copies_in_the_project_and_a_parent_folder_both_load()
    {
        var repo = _home.Dir("dev/mono");
        Directory.CreateDirectory(Path.Combine(repo, ".git"));
        Skill(repo, "lint");
        var web = Project("dev/mono/web");
        Skill(web, "lint");

        var (scan, project) = await Scan(web);

        EffectiveSetBuilder.Build(scan, project).Entries.Should().HaveCount(2).And.OnlyContain(e => e.Availability == Availability.Active);
    }
}

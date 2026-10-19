using FluentAssertions;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class SkillOverridesTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();
    private readonly string _app;

    public SkillOverridesTests()
    {
        _app = _home.Project("dev/app");
        _home.RegisterProjectFolder(_app);
    }

    public void Dispose() => _home.Dispose();

    private static string Doc(string name) => $"---\nname: {name}\ndescription: d\n---\nbody\n";

    private void UserSettings(string json) => _home.File(_home.Paths.SettingsJson, json);

    private void ProjectSettings(string file, string json) => _home.File(Path.Combine(_app, ".claude", file), json);

    private async Task<(ScanResult Scan, SkillSource Project)> Scan()
    {
        var scan = await SkillScanner.ScanAsync(_home.Paths);
        return (scan, scan.Sources.Single(s => s.Id == "project:" + _app));
    }

    [Fact]
    public async Task Local_beats_shared_beats_user_settings_key_by_key()
    {
        _home.ProjectSkill(_app, "x", Doc("x"));
        UserSettings("""{ "skillOverrides": { "deploy": "off", "notes": "name-only", "lint": "off" } }""");
        ProjectSettings("settings.json", """{ "skillOverrides": { "deploy": "on", "notes": "user-invocable-only" } }""");
        ProjectSettings("settings.local.json", """{ "skillOverrides": { "notes": "off" } }""");

        var (scan, project) = await Scan();

        scan.Overrides.Resolve("deploy", project).Should().Match<ResolvedVisibility>(r => r.Value == SkillVisibility.On && r.From!.Scope == SettingsScope.Project);
        scan.Overrides.Resolve("notes", project).Should().Match<ResolvedVisibility>(r => r.Value == SkillVisibility.Off && r.From!.Scope == SettingsScope.Local);
        scan.Overrides.Resolve("lint", project).Should().Match<ResolvedVisibility>(r => r.Value == SkillVisibility.Off && r.From!.Scope == SettingsScope.User);
        scan.Overrides.Resolve("other", project).Should().Be(ResolvedVisibility.Default);
        scan.Overrides.Resolve("notes", null).Value.Should().Be(SkillVisibility.NameOnly);
    }

    [Fact]
    public async Task Unknown_values_are_ignored_and_broken_project_files_are_reported()
    {
        _home.ProjectSkill(_app, "x", Doc("x"));
        UserSettings("""{ "skillOverrides": { "deploy": "hidden", "notes": 3 } }""");
        ProjectSettings("settings.local.json", "{ not json");

        var (scan, project) = await Scan();

        scan.Overrides.Resolve("deploy", project).Should().Be(ResolvedVisibility.Default);
        scan.Issues.Should().ContainSingle(i => i.Path.EndsWith("settings.local.json") && i.Message.StartsWith("Cannot read Claude Code settings"));
    }

    [Fact]
    public async Task Personal_skills_are_written_to_user_settings_and_project_skills_to_local_settings()
    {
        _home.GlobalSkill("g", Doc("g"));
        _home.ProjectSkill(_app, "p", Doc("p"));
        _home.GlobalCommand("ship", "body");

        var (scan, _) = await Scan();

        scan.Overrides.WritableLayerFor(scan.Skills.Single(s => s.Name == "g"))!.Path.Should().Be(_home.Paths.SettingsJson);
        scan.Overrides.WritableLayerFor(scan.Skills.Single(s => s.Name == "p"))!.Path.Should().Be(Path.Combine(_app, ".claude", "settings.local.json"));
        scan.Overrides.WritableLayerFor(scan.Skills.Single(s => s.Name == "ship")).Should().BeNull();
    }

    [Fact]
    public async Task The_effective_set_marks_skills_turned_off_in_the_project()
    {
        _home.GlobalSkill("g", Doc("g"));
        _home.ProjectSkill(_app, "p", Doc("p"));
        ProjectSettings("settings.json", """{ "skillOverrides": { "g": "off", "p": "name-only" } }""");

        var (scan, project) = await Scan();
        var set = EffectiveSetBuilder.Build(scan, project);

        var g = set.Entries.Single(e => e.Skill.Name == "g");
        g.IsTurnedOff.Should().BeTrue();
        g.Label(s => s.DisplayName).Should().Be("off");
        set.Entries.Single(e => e.Skill.Name == "p").Label(s => s.DisplayName).Should().Be("name only");
    }

    [Fact]
    public async Task Duplicate_keys_keep_the_last_value_like_claude_code()
    {
        UserSettings("""{ "skillOverrides": { "deploy": "off", "deploy": "name-only" } }""");

        var (scan, _) = await Scan();

        scan.Overrides.Resolve("deploy", null).Value.Should().Be(SkillVisibility.NameOnly);
        scan.Issues.Should().BeEmpty();
    }

    [Fact]
    public async Task Turning_on_writes_an_explicit_on_only_when_a_lower_file_would_hide_the_skill()
    {
        _home.ProjectSkill(_app, "p", Doc("p"));
        _home.ProjectSkill(_app, "q", Doc("q"));
        _home.GlobalSkill("g", Doc("g"));
        ProjectSettings("settings.json", """{ "skillOverrides": { "p": "off" } }""");

        var (scan, _) = await Scan();
        SkillVisibility? EntryFor(string name)
        {
            var skill = scan.Skills.Single(s => s.Name == name);
            return scan.Overrides.EntryFor(skill, scan.Overrides.WritableLayerFor(skill)!, SkillVisibility.On);
        }

        EntryFor("p").Should().Be(SkillVisibility.On);
        EntryFor("q").Should().BeNull();
        EntryFor("g").Should().BeNull();
    }

    [Fact]
    public async Task Plugin_skills_ignore_overrides()
    {
        var plugin = _home.InstallPlugin("m", "kit", "1");
        _home.File(Path.Combine(plugin, "skills", "k", "SKILL.md"), Doc("k"));
        UserSettings("""{ "skillOverrides": { "k": "off" } }""");

        var (scan, project) = await Scan();

        EffectiveSetBuilder.Build(scan, project).Entries.Single().IsTurnedOff.Should().BeFalse();
    }
}

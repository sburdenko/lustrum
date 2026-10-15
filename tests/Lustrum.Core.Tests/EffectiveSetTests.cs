using FluentAssertions;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class EffectiveSetTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();
    private readonly string _app;
    private readonly string _other;

    public EffectiveSetTests()
    {
        _app = _home.Project("dev/app");
        _other = _home.Project("dev/other");
        _home.RegisterProjectFolder(_app);
        _home.RegisterProjectFolder(_other);
    }

    public void Dispose() => _home.Dispose();

    private static string Doc(string name, string description = "d") =>
        $"---\nname: {name}\ndescription: {description}\n---\nbody\n";

    private async Task<EffectiveSet> BuildFor(string projectPath)
    {
        var scan = await SkillScanner.ScanAsync(_home.Paths);
        var project = scan.Sources.Single(s => s.Id == "project:" + projectPath);
        return EffectiveSetBuilder.Build(scan, project);
    }

    [Fact]
    public async Task Contains_project_global_and_plugin_skills_but_not_other_projects()
    {
        _home.GlobalSkill("g", Doc("g"));
        _home.ProjectSkill(_app, "p", Doc("p"));
        _home.ProjectSkill(_other, "elsewhere", Doc("elsewhere"));
        var plugin = _home.InstallPlugin("m", "kit", "1");
        _home.File(Path.Combine(plugin, "skills", "k", "SKILL.md"), Doc("k"));

        var set = await BuildFor(_app);

        set.Entries.Select(e => e.Skill.FullName).Should().BeEquivalentTo("g", "p", "kit:k");
        set.Entries.Should().OnlyContain(e => e.Availability == Availability.Active);
    }

    [Fact]
    public async Task Project_skill_shadows_global_skill_with_the_same_name()
    {
        _home.GlobalSkill("review", Doc("review", "global"));
        _home.ProjectSkill(_app, "review", Doc("review", "project"));

        var set = await BuildFor(_app);

        var winner = set.Entries.Single(e => e.Availability == Availability.Active);
        var loser = set.Entries.Single(e => e.Availability == Availability.Shadowed);
        winner.Skill.Source.Kind.Should().Be(SourceKind.Project);
        winner.Overrides.Should().ContainSingle().Which.Should().Be(loser.Skill);
        loser.Skill.Source.Kind.Should().Be(SourceKind.Global);
        loser.ShadowedBy.Should().Be(winner.Skill);
    }

    [Fact]
    public async Task Same_name_in_another_project_does_not_shadow()
    {
        _home.GlobalSkill("review", Doc("review"));
        _home.ProjectSkill(_other, "review", Doc("review"));

        var set = await BuildFor(_app);

        set.Entries.Should().ContainSingle().Which.Availability.Should().Be(Availability.Active);
    }

    [Fact]
    public async Task Disabled_plugins_are_not_part_of_the_set()
    {
        var on = _home.InstallPlugin("m", "on", "1");
        var off = _home.InstallPlugin("m", "off", "1");
        _home.File(Path.Combine(on, "skills", "a", "SKILL.md"), Doc("a"));
        _home.File(Path.Combine(off, "skills", "b", "SKILL.md"), Doc("b"));
        _home.EnablePlugins(("on@m", true), ("off@m", false));

        var set = await BuildFor(_app);

        set.Entries.Select(e => e.Skill.FullName).Should().Equal("on:a");
    }

    [Fact]
    public async Task Plugin_skill_never_shadows_or_is_shadowed()
    {
        _home.GlobalSkill("a", Doc("a"));
        var plugin = _home.InstallPlugin("m", "kit", "1");
        _home.File(Path.Combine(plugin, "skills", "a", "SKILL.md"), Doc("a"));

        var set = await BuildFor(_app);

        set.Entries.Should().HaveCount(2).And.OnlyContain(e => e.Availability == Availability.Active);
    }

    [Fact]
    public async Task Project_is_found_by_its_folder_path()
    {
        var scan = await SkillScanner.ScanAsync(_home.Paths);

        EffectiveSetBuilder.FindProject(scan, _app)!.Id.Should().Be("project:" + _app);
        EffectiveSetBuilder.FindProject(scan, _app + Path.DirectorySeparatorChar)!.Id.Should().Be("project:" + _app);
        EffectiveSetBuilder.FindProject(scan, Path.Combine(_app, "nowhere")).Should().BeNull();
    }

    [Fact]
    public async Task Availability_labels_name_the_winning_and_losing_sources()
    {
        _home.GlobalSkill("review", Doc("review"));
        _home.ProjectSkill(_app, "review", Doc("review"));
        _home.GlobalSkill("solo", Doc("solo"));

        var set = await BuildFor(_app);

        string Label(SkillSource s) => s.Kind == SourceKind.Global ? "Global" : s.DisplayName;
        set.Entries.Single(e => e.Availability == Availability.Shadowed).Label(Label).Should().Be("shadowed by app");
        set.Entries.Single(e => e.Overrides.Count > 0).Label(Label).Should().Be("overrides Global");
        set.Entries.Single(e => e.Skill.Name == "solo").Label(Label).Should().BeNull();
    }

    [Fact]
    public async Task Find_returns_the_entry_for_a_skill_id()
    {
        _home.GlobalSkill("g", Doc("g"));

        var set = await BuildFor(_app);

        var skill = set.Entries.Single().Skill;
        set.Find(skill.Id).Should().NotBeNull();
        set.Find("nope").Should().BeNull();
    }
}

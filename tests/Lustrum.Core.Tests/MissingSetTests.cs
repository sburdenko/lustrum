using FluentAssertions;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class MissingSetTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();
    private readonly string _app;
    private readonly string _other;

    public MissingSetTests()
    {
        _app = _home.Project("dev/app");
        _other = _home.Project("dev/other");
        _home.RegisterProjectFolder(_app);
        _home.RegisterProjectFolder(_other);
    }

    public void Dispose() => _home.Dispose();

    private static string Doc(string name) => $"---\nname: {name}\ndescription: d\n---\nbody\n";

    private static string Label(SkillSource source) => source.Kind == SourceKind.Global ? "Global" : source.Namespace ?? source.DisplayName;

    private async Task<MissingSet> BuildFor(string projectPath)
    {
        var scan = await SkillScanner.ScanAsync(_home.Paths);
        return MissingSetBuilder.Build(scan, scan.Sources.Single(s => s.Id == "project:" + projectPath));
    }

    [Fact]
    public async Task Skills_of_other_projects_are_missing_and_loaded_ones_are_not()
    {
        _home.GlobalSkill("g", Doc("g"));
        _home.ProjectSkill(_app, "mine", Doc("mine"));
        _home.ProjectSkill(_other, "theirs", Doc("theirs"));

        var set = await BuildFor(_app);

        var entry = set.Entries.Should().ContainSingle().Subject;
        entry.Skill.Name.Should().Be("theirs");
        entry.Reason(Label).Should().BeNull();
    }

    [Fact]
    public async Task Disabled_plugin_skills_are_missing_everywhere()
    {
        var off = _home.InstallPlugin("m", "off", "1");
        _home.File(Path.Combine(off, "skills", "x", "SKILL.md"), Doc("x"));
        _home.EnablePlugins(("off@m", false));

        var set = await BuildFor(_app);

        set.Entries.Single().Reason(Label).Should().Be("plugin disabled");
    }

    [Fact]
    public async Task A_missing_copy_says_where_the_loaded_one_comes_from()
    {
        _home.GlobalSkill("review", Doc("review"));
        _home.ProjectSkill(_other, "review", Doc("review"));

        var set = await BuildFor(_app);

        var entry = set.Entries.Single();
        entry.LoadedHere!.Source.Kind.Should().Be(SourceKind.Global);
        entry.Reason(Label).Should().Be("loaded here from Global");
    }

    [Fact]
    public async Task A_disabled_plugin_skill_also_says_where_the_loaded_copy_comes_from()
    {
        var off = _home.InstallPlugin("m1", "kit", "1");
        _home.File(Path.Combine(off, "skills", "x", "SKILL.md"), Doc("x"));
        var on = _home.InstallPlugin("m2", "kit", "1");
        _home.File(Path.Combine(on, "skills", "x", "SKILL.md"), Doc("x"));
        _home.EnablePlugins(("kit@m1", false));

        var set = await BuildFor(_app);

        set.Entries.Single().Reason(Label).Should().Be("plugin disabled; loaded here from kit");
    }

    [Fact]
    public void Built_in_skills_are_always_loaded()
    {
        var builtIn = new SkillSource("builtin", SourceKind.BuiltIn, "Built-in", "/none", null, true);
        var skill = new Skill("builtin#x", "x", "x", "d", SkillKind.Skill, builtIn, "/none/x", null,
            new Dictionary<string, string>(), [], [], 0, DateTimeOffset.UnixEpoch, Health.Ok, []);

        new Lustrum.Core.Indexing.EffectiveScope("project:/a").Includes(skill).Should().BeTrue();
        new Lustrum.Core.Indexing.MissingScope("project:/a").Includes(skill).Should().BeFalse();
    }

    [Fact]
    public async Task Find_returns_entries_by_skill_id()
    {
        _home.ProjectSkill(_other, "theirs", Doc("theirs"));

        var set = await BuildFor(_app);

        set.Find(set.Entries.Single().Skill.Id).Should().NotBeNull();
        set.Find("nope").Should().BeNull();
    }
}

using FluentAssertions;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;
using Lustrum.Core.Watching;

namespace Lustrum.Core.Tests;

public class SyncedSourcesTests
{
    private const string Account = "acc-1";

    private static string Doc(string name) => $"---\nname: {name}\ndescription: d\n---\nbody\n";

    [Fact]
    public async Task Account_skills_are_listed_under_the_anthropic_skills_prefix()
    {
        using var home = new FakeClaudeHome();
        home.SyncedSkill(Account, "stop-slop", Doc("stop-slop"));
        home.SyncedSkill(Account, "xlsx", Doc("xlsx"));

        var result = await SkillScanner.ScanAsync(home.Paths);

        var source = result.Sources.Single(s => s.Kind == SourceKind.Synced);
        source.Namespace.Should().Be("anthropic-skills");
        result.Skills.Select(s => s.FullName).Should().BeEquivalentTo("anthropic-skills:stop-slop", "anthropic-skills:xlsx");
        result.Skills.Should().OnlyContain(s => s.Tags.Contains("synced") && !s.Tags.Contains("plugin"));
    }

    [Fact]
    public async Task Staging_folders_and_manifest_are_ignored()
    {
        using var home = new FakeClaudeHome();
        home.SyncedSkill(Account, "real", Doc("real"));
        home.SyncedSkill(Account, ".staging", Doc("staging"));
        home.File(Path.Combine(home.ClaudeDir, "skills", "synced", Account, "manifest.json"), "{}");
        home.SyncedSkill(".bucket-" + Account, "ghost", Doc("ghost"));

        var result = await SkillScanner.ScanAsync(home.Paths);

        result.Skills.Select(s => s.Name).Should().Equal("real");
    }

    [Fact]
    public async Task Synced_plugin_uses_the_newest_generation_and_its_manifest_name()
    {
        using var home = new FakeClaudeHome();
        var old = home.SyncedPlugin(Account, "design", "design");
        home.File(Path.Combine(old, "skills", "old-skill", "SKILL.md"), Doc("old-skill"));
        var current = home.SyncedPlugin(Account, "design~g2", "design");
        home.File(Path.Combine(current, "skills", "critique", "SKILL.md"), Doc("critique"));
        var byId = home.SyncedPlugin(Account, "7ede45e8-uuid", "meshy");
        home.File(Path.Combine(byId, "commands", "make.md"), "Make a model");

        var result = await SkillScanner.ScanAsync(home.Paths);

        result.Skills.Select(s => s.FullName).Should().BeEquivalentTo("design:critique", "meshy:make");
        result.Sources.Where(s => s.Kind == SourceKind.Synced).Select(s => s.Namespace).Should().BeEquivalentTo("design", "meshy");
    }

    [Fact]
    public async Task Account_skill_with_a_global_name_is_not_a_conflict()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("docs", Doc("docs"));
        home.SyncedSkill(Account, "docs", Doc("docs"));

        var result = await SkillScanner.ScanAsync(home.Paths);

        result.Conflicts.Should().BeEmpty();
        result.Skills.Should().OnlyContain(s => !s.Tags.Contains("duplicate"));
    }

    [Fact]
    public async Task Account_skills_are_part_of_every_effective_set()
    {
        using var home = new FakeClaudeHome();
        var project = home.Project("dev/app");
        home.RegisterProjectFolder(project);
        home.SyncedSkill(Account, "xlsx", Doc("xlsx"));

        var scan = await SkillScanner.ScanAsync(home.Paths);
        var set = EffectiveSetBuilder.Build(scan, scan.Sources.Single(s => s.Kind == SourceKind.Project));

        set.Entries.Select(e => e.Skill.FullName).Should().Equal("anthropic-skills:xlsx");
    }

    [Fact]
    public async Task Synced_plugins_are_watched_through_their_skill_folders_and_account_folder()
    {
        using var home = new FakeClaudeHome();
        var plugin = home.SyncedPlugin(Account, "design~g2", "design");
        home.File(Path.Combine(plugin, "skills", "a", "SKILL.md"), Doc("a"));
        var scan = await SkillScanner.ScanAsync(home.Paths);

        var targets = WatchTargets.From(scan, home.Paths);

        targets.Should().Contain(new WatchTarget(Path.Combine(plugin, "skills"), Recursive: true));
        targets.Should().Contain(new WatchTarget(Path.GetDirectoryName(plugin)!, Recursive: false));
        targets.Should().NotContain(t => t.Path == home.Paths.SyncedPluginsDir && t.Recursive);
    }

    [Theory]
    [InlineData("tool~g99999999999")]
    [InlineData("tool~g\u0663")]
    public async Task Odd_generation_suffixes_do_not_break_the_scan(string folder)
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("g", Doc("g"));
        home.SyncedPlugin(Account, System.Text.RegularExpressions.Regex.Unescape(folder), "tool");

        var result = await SkillScanner.ScanAsync(home.Paths);

        result.Skills.Should().ContainSingle(s => s.Name == "g");
    }

    [Fact]
    public async Task A_bare_folder_and_g1_resolve_deterministically()
    {
        using var home = new FakeClaudeHome();
        var bare = home.SyncedPlugin(Account, "kit", "kit");
        home.File(Path.Combine(bare, "skills", "from-bare", "SKILL.md"), Doc("from-bare"));
        var g1 = home.SyncedPlugin(Account, "kit~g1", "kit");
        home.File(Path.Combine(g1, "skills", "from-g1", "SKILL.md"), Doc("from-g1"));

        var first = (await SkillScanner.ScanAsync(home.Paths)).Skills.Select(s => s.Name).ToList();
        var second = (await SkillScanner.ScanAsync(home.Paths)).Skills.Select(s => s.Name).ToList();

        first.Should().ContainSingle().And.Equal(second);
    }

    [Fact]
    public async Task A_plugin_named_like_the_account_bucket_keeps_its_own_source()
    {
        using var home = new FakeClaudeHome();
        home.SyncedSkill(Account, "a", Doc("a"));
        var plugin = home.SyncedPlugin(Account, "anthropic-skills", "anthropic-skills");
        home.File(Path.Combine(plugin, "skills", "b", "SKILL.md"), Doc("b"));

        var result = await SkillScanner.ScanAsync(home.Paths);

        result.Sources.Count(s => s.Kind == SourceKind.Synced).Should().Be(2);
        result.Skills.Should().HaveCount(2);
    }

    [Fact]
    public async Task Same_full_name_from_two_sources_is_a_conflict()
    {
        using var home = new FakeClaudeHome();
        home.SyncedSkill("acc-1", "foo", Doc("foo"));
        home.SyncedSkill("acc-2", "foo", Doc("foo"));
        var installed = home.InstallPlugin("m", "design", "1");
        home.File(Path.Combine(installed, "skills", "critique", "SKILL.md"), Doc("critique"));
        var synced = home.SyncedPlugin(Account, "design", "design");
        home.File(Path.Combine(synced, "skills", "critique", "SKILL.md"), Doc("critique"));

        var result = await SkillScanner.ScanAsync(home.Paths);

        result.Conflicts.Select(c => c.Name).Should().BeEquivalentTo("anthropic-skills:foo", "design:critique");
        result.Skills.Should().OnlyContain(s => s.Tags.Contains("duplicate"));
    }

    [Theory]
    [InlineData("a:b")]
    [InlineData("a/b")]
    [InlineData("   ")]
    public async Task Unusable_manifest_names_fall_back_to_the_folder(string name)
    {
        using var home = new FakeClaudeHome();
        var plugin = home.SyncedPlugin(Account, "kit~g3", name);
        home.File(Path.Combine(plugin, "skills", "s", "SKILL.md"), Doc("s"));

        var result = await SkillScanner.ScanAsync(home.Paths);

        result.Skills.Single().FullName.Should().Be("kit:s");
    }

    [Fact]
    public async Task Regular_plugins_carry_their_namespace_too()
    {
        using var home = new FakeClaudeHome();
        var plugin = home.InstallPlugin("m", "kit", "1");
        home.File(Path.Combine(plugin, "skills", "a", "SKILL.md"), Doc("a"));

        var result = await SkillScanner.ScanAsync(home.Paths);

        result.Sources.Single(s => s.Kind == SourceKind.Plugin).Namespace.Should().Be("kit");
        result.Skills.Single().FullName.Should().Be("kit:a");
    }
}

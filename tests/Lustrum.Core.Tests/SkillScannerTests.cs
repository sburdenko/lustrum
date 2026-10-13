using FluentAssertions;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class SkillScannerTests
{
    private const string Valid = "---\nname: {0}\ndescription: {1}\n---\nbody of {0}\n";

    private static string Doc(string name, string description) => string.Format(Valid, name, description);

    private static Task<ScanResult> Scan(FakeClaudeHome home) => SkillScanner.ScanAsync(home.Paths);

    [Fact]
    public async Task Scans_global_skills_and_commands()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("design", Doc("design", "Design things"));
        home.GlobalCommand("deploy", "---\ndescription: Ship it\n---\nrun deploy");

        var result = await Scan(home);

        result.Skills.Should().HaveCount(2);
        var skill = result.Skills.Single(s => s.Kind == SkillKind.Skill);
        skill.Name.Should().Be("design");
        skill.Description.Should().Be("Design things");
        skill.Source.Kind.Should().Be(SourceKind.Global);
        skill.Health.Should().Be(Health.Ok);
        var command = result.Skills.Single(s => s.Kind == SkillKind.Command);
        command.Name.Should().Be("deploy");
        command.Health.Should().Be(Health.Ok);
        command.InvocationName.Should().Be("/deploy");
    }

    [Fact]
    public async Task Name_comes_from_frontmatter_otherwise_folder()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("folder-name", Doc("declared-name", "d"));
        home.GlobalSkill("no-name", "---\ndescription: d\n---\nbody");

        var result = await Scan(home);

        result.Skills.Select(s => s.Name).Should().BeEquivalentTo("declared-name", "no-name");
        result.Skills.Single(s => s.Name == "no-name").Health.Should().Be(Health.MissingName);
    }

    [Fact]
    public async Task Skill_without_frontmatter_uses_folder_and_first_body_line()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("raw", "\n\nFirst real line\nsecond");

        var result = await Scan(home);

        var skill = result.Skills.Single();
        skill.Health.Should().Be(Health.NoFrontmatter);
        skill.Name.Should().Be("raw");
        skill.Description.Should().Be("First real line");
    }

    [Fact]
    public async Task Missing_description_is_flagged()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("nodesc", "---\nname: nodesc\n---\nbody");

        var result = await Scan(home);

        result.Skills.Single().Health.Should().Be(Health.MissingDescription);
        result.Skills.Single().IsBroken.Should().BeTrue();
    }

    [Fact]
    public async Task Overlong_description_warns_but_is_not_broken()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("long", Doc("long", new string('x', 1100)));

        var result = await Scan(home);

        var skill = result.Skills.Single();
        skill.Health.Should().Be(Health.DescriptionTooLong);
        skill.IsBroken.Should().BeFalse();
    }

    [Fact]
    public async Task Broken_yaml_is_recorded_as_issue_and_skill_is_still_listed()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("bad", "---\nname: [oops\n---\nbody");

        var result = await Scan(home);

        result.Skills.Single().Health.Should().Be(Health.ParseError);
        result.Issues.Should().ContainSingle(i => i.Path.EndsWith("SKILL.md"));
    }

    [Fact]
    public async Task Commands_without_frontmatter_are_valid()
    {
        using var home = new FakeClaudeHome();
        home.GlobalCommand("plain", "Just run the thing\nmore");

        var result = await Scan(home);

        var command = result.Skills.Single();
        command.Health.Should().Be(Health.Ok);
        command.Description.Should().Be("Just run the thing");
    }

    [Fact]
    public async Task Only_one_level_under_skills_is_scanned()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("top", Doc("top", "d"));
        home.File(Path.Combine(home.ClaudeDir, "skills", "group", "nested", "SKILL.md"), Doc("nested", "d"));

        var result = await Scan(home);

        result.Skills.Select(s => s.Name).Should().Equal("top");
    }

    [Fact]
    public async Task Sibling_files_are_listed_without_skill_md()
    {
        using var home = new FakeClaudeHome();
        var skill = home.GlobalSkill("rich", Doc("rich", "d"));
        var folder = Path.GetDirectoryName(skill)!;
        home.File(Path.Combine(folder, "scripts", "run.sh"), "#!/bin/sh");
        home.File(Path.Combine(folder, "templates", "a.txt"), "a");

        var result = await Scan(home);

        result.Skills.Single().Files.Should().BeEquivalentTo(
            Path.Combine("scripts", "run.sh"),
            Path.Combine("templates", "a.txt"));
    }

    [Fact]
    public async Task Body_is_not_loaded_during_scan_but_reader_loads_it()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("lazy", Doc("lazy", "d"));

        var result = await Scan(home);

        var skill = result.Skills.Single();
        skill.Body.Should().BeNull();
        SkillBodyReader.Read(skill).Should().Contain("body of lazy");
    }

    [Fact]
    public async Task Projects_are_discovered_from_projects_folder_and_decoded()
    {
        using var home = new FakeClaudeHome();
        var project = home.Project("dev/tossivo");
        home.ProjectSkill(project, "local", Doc("local", "Local skill"));
        home.RegisterProjectFolder(project);

        var result = await Scan(home);

        var source = result.Sources.Single(s => s.Kind == SourceKind.Project);
        source.Exists.Should().BeTrue();
        source.DisplayName.Should().Be("tossivo");
        source.Id.Should().Be("project:" + project);
        result.Skills.Single(s => s.Name == "local").Source.Should().Be(source);
    }

    [Fact]
    public async Task Missing_project_stays_in_list_as_ghost()
    {
        using var home = new FakeClaudeHome();
        home.RegisterProjectFolderRaw("-gone-forever-project");

        var result = await Scan(home);

        var ghost = result.Sources.Single(s => s.Kind == SourceKind.Project);
        ghost.Exists.Should().BeFalse();
        ghost.DisplayName.Should().Contain("gone");
    }

    [Fact]
    public async Task Claude_json_projects_are_merged_without_duplicates()
    {
        using var home = new FakeClaudeHome();
        var a = home.Project("dev/alpha");
        var b = home.Project("dev/beta");
        home.RegisterProjectFolder(a);
        home.RegisterInClaudeJson(a, b);

        var result = await Scan(home);

        result.Sources.Where(s => s.Kind == SourceKind.Project).Select(s => s.RootPath)
            .Should().BeEquivalentTo(Path.Combine(a, ".claude"), Path.Combine(b, ".claude"));
    }

    [Fact]
    public async Task Claude_json_resolves_ghost_folder_to_exact_path()
    {
        using var home = new FakeClaudeHome();
        var ghostPath = Path.Combine(Path.GetTempPath(), "lustrum-ghost", "removed-project");
        home.RegisterProjectFolderRaw(ProjectPathDecoder.Encode(ghostPath));
        home.RegisterInClaudeJson(ghostPath);

        var result = await Scan(home);

        var ghost = result.Sources.Single(s => s.Kind == SourceKind.Project);
        ghost.Exists.Should().BeFalse();
        ghost.DisplayName.Should().Be("removed-project");
    }

    [Fact]
    public async Task Home_directory_project_is_not_duplicated_as_global()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("one", Doc("one", "d"));
        home.RegisterProjectFolder(home.Home);

        var result = await Scan(home);

        result.Sources.Should().NotContain(s => s.Kind == SourceKind.Project);
        result.Skills.Should().ContainSingle();
    }

    [Fact]
    public async Task Plugins_come_from_installed_list_and_orphaned_versions_are_ignored()
    {
        using var home = new FakeClaudeHome();
        var current = home.InstallPlugin("market", "unity", "2.0.0");
        home.File(Path.Combine(current, "skills", "ui", "SKILL.md"), Doc("ui", "Unity UI"));
        home.File(Path.Combine(current, "commands", "build.md"), "Build it");
        var old = home.InstallPlugin("market", "unity", "1.0.0", orphaned: true, register: false);
        home.File(Path.Combine(old, "skills", "stale", "SKILL.md"), Doc("stale", "Old"));

        var result = await Scan(home);

        var plugin = result.Sources.Single(s => s.Kind == SourceKind.Plugin);
        plugin.Id.Should().Be("plugin:market/unity");
        result.Skills.Select(s => s.FullName).Should().BeEquivalentTo("unity:ui", "unity:build");
    }

    [Fact]
    public async Task Plugin_skills_without_registry_are_found_in_cache_skipping_orphans()
    {
        using var home = new FakeClaudeHome();
        var live = home.InstallPlugin("market", "sales", "1.0.0", register: false);
        home.File(Path.Combine(live, "skills", "forecast", "SKILL.md"), Doc("forecast", "d"));
        var dead = home.InstallPlugin("market", "sales", "0.9.0", orphaned: true, register: false);
        home.File(Path.Combine(dead, "skills", "ghost", "SKILL.md"), Doc("ghost", "d"));

        var result = await Scan(home);

        result.Skills.Select(s => s.FullName).Should().Equal("sales:forecast");
    }

    [Fact]
    public async Task Same_name_in_global_and_project_is_a_conflict_but_plugins_never_conflict()
    {
        using var home = new FakeClaudeHome();
        var project = home.Project("dev/app");
        home.RegisterProjectFolder(project);
        home.GlobalSkill("shared", Doc("shared", "global"));
        home.ProjectSkill(project, "shared", Doc("shared", "project"));
        var plugin = home.InstallPlugin("m", "p", "1");
        home.File(Path.Combine(plugin, "skills", "shared", "SKILL.md"), Doc("shared", "plugin"));

        var result = await Scan(home);

        var conflict = result.Conflicts.Should().ContainSingle().Subject;
        conflict.Name.Should().Be("shared");
        conflict.Skills.Should().HaveCount(2);
        conflict.Skills.Should().NotContain(s => s.Source.Kind == SourceKind.Plugin);
        result.Skills.Where(s => s.Name == "shared" && s.Source.Kind != SourceKind.Plugin)
            .Should().OnlyContain(s => s.Tags.Contains("duplicate"));
    }

    [Fact]
    public async Task Tags_are_computed()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("sb-mine", Doc("sb-mine", "d"));
        home.GlobalSkill("bad", "no frontmatter");
        home.GlobalCommand("cmd", "x");
        var plugin = home.InstallPlugin("m", "p", "1");
        home.File(Path.Combine(plugin, "skills", "s", "SKILL.md"), Doc("s", "d"));

        var result = await Scan(home);

        Tags(result, "sb-mine").Should().Contain("sb-");
        Tags(result, "bad").Should().Contain("broken");
        Tags(result, "cmd").Should().Contain("command");
        result.Skills.Single(s => s.Source.Kind == SourceKind.Plugin).Tags.Should().Contain("plugin");
    }

    [Fact]
    public async Task Scan_is_stable_and_sorted_by_name()
    {
        using var home = new FakeClaudeHome();
        foreach (var name in new[] { "zeta", "alpha", "mid" })
        {
            home.GlobalSkill(name, Doc(name, "d"));
        }

        var result = await Scan(home);

        result.Skills.Select(s => s.Name).Should().Equal("alpha", "mid", "zeta");
        result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task Empty_home_yields_empty_result()
    {
        using var home = new FakeClaudeHome();

        var result = await Scan(home);

        result.Skills.Should().BeEmpty();
        result.Conflicts.Should().BeEmpty();
    }

    [Fact]
    public async Task Colliding_claude_json_keys_do_not_break_the_scan()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("one", Doc("one", "d"));
        var root = Path.Combine(Path.GetTempPath(), "lustrum-collide");
        home.RegisterInClaudeJson(Path.Combine(root, "a-b"), Path.Combine(root, "a", "b"));

        var result = await Scan(home);

        result.Skills.Should().ContainSingle();
        result.Sources.Count(s => s.Kind == SourceKind.Project).Should().Be(2);
    }

    [Fact]
    public async Task Skills_declaring_the_same_name_in_one_source_get_distinct_ids()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("first", Doc("same", "a"));
        home.GlobalSkill("second", Doc("same", "b"));

        var result = await Scan(home);

        result.Skills.Select(s => s.Id).Distinct().Should().HaveCount(2);
    }

    [Fact]
    public async Task Skill_id_is_stable_when_the_declared_name_changes()
    {
        using var home = new FakeClaudeHome();
        var path = home.GlobalSkill("folder", Doc("before", "d"));
        var before = (await Scan(home)).Skills.Single().Id;

        home.File(path, Doc("after", "d"));
        var after = (await Scan(home)).Skills.Single().Id;

        after.Should().Be(before);
    }

    [Fact]
    public async Task Namespaced_commands_are_named_with_colons()
    {
        using var home = new FakeClaudeHome();
        home.File(Path.Combine(home.ClaudeDir, "commands", "git", "commit.md"), "Commit it");

        var result = await Scan(home);

        var command = result.Skills.Single();
        command.Name.Should().Be("git:commit");
        command.InvocationName.Should().Be("/git:commit");
    }

    [Fact]
    public async Task Frontmatter_longer_than_the_old_8kb_head_is_still_parsed()
    {
        using var home = new FakeClaudeHome();
        var padding = string.Join("\n", Enumerable.Range(0, 400).Select(i => $"key{i}: {new string('v', 30)}"));
        home.GlobalSkill("big", $"---\nname: big\ndescription: d\n{padding}\n---\nbody");

        var result = await Scan(home);

        result.Skills.Single().Health.Should().Be(Health.Ok);
    }

    [Theory]
    [InlineData("""{ "plugins": "oops" }""")]
    [InlineData("""{ "plugins": { "a@m": ["not-an-object"] } }""")]
    [InlineData("""{ "plugins": { "a@m": [{ "installPath": 5 }] } }""")]
    [InlineData("""{ "plugins": { "a@m": [{ "installPath": "relative/path" }] } }""")]
    public async Task Malformed_plugin_registry_does_not_break_the_scan(string registry)
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("one", Doc("one", "d"));
        home.File(home.Paths.InstalledPluginsJson, registry);

        var result = await Scan(home);

        result.Skills.Should().ContainSingle();
        result.Sources.Should().NotContain(s => s.Kind == SourceKind.Plugin);
    }

    [Fact]
    public async Task Unresolvable_project_is_never_resolved_against_the_working_directory()
    {
        using var home = new FakeClaudeHome();
        var raw = "-lustrum-cwd-probe";
        var inCwd = Path.Combine(Directory.GetCurrentDirectory(), raw);
        Directory.CreateDirectory(Path.Combine(inCwd, ".claude", "skills", "x"));
        try
        {
            home.File(Path.Combine(inCwd, ".claude", "skills", "x", "SKILL.md"), Doc("x", "d"));
            home.RegisterProjectFolderRaw(raw);

            var result = await Scan(home);

            result.Sources.Single(s => s.Kind == SourceKind.Project).Exists.Should().BeFalse();
            result.Skills.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(inCwd, recursive: true);
        }
    }

    private static IReadOnlyList<string> Tags(ScanResult result, string name) =>
        result.Skills.Single(s => s.Name == name).Tags;
}

using FluentAssertions;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class SourceDiscoveryTests
{
    private const string Valid = "---\nname: {0}\ndescription: d\n---\nbody\n";

    private static string Doc(string name) => string.Format(Valid, name);

    private static ScanOptions WithRoots(params string[] roots) =>
        ScanOptions.Default with { ExtraRoots = roots };

    [Fact]
    public async Task Extra_roots_find_projects_claude_code_never_opened()
    {
        using var home = new FakeClaudeHome();
        var root = home.Dir("code");
        var shallow = home.Project("code/alpha");
        var deep = home.Project("code/clients/acme/beta");
        home.ProjectSkill(shallow, "a", Doc("a"));
        home.ProjectSkill(deep, "b", Doc("b"));

        var result = await SkillScanner.ScanAsync(home.Paths, WithRoots(root));

        result.Sources.Where(s => s.Kind == SourceKind.Project).Select(s => s.DisplayName)
            .Should().BeEquivalentTo("alpha", "beta");
        result.Skills.Select(s => s.Name).Should().BeEquivalentTo("a", "b");
    }

    [Fact]
    public async Task Extra_roots_respect_depth_limit()
    {
        using var home = new FakeClaudeHome();
        var root = home.Dir("code");
        var tooDeep = home.Project("code/1/2/3/4/5/deep");
        home.ProjectSkill(tooDeep, "x", Doc("x"));

        var result = await SkillScanner.ScanAsync(home.Paths, WithRoots(root) with { SearchDepth = 3 });

        result.Sources.Should().NotContain(s => s.Kind == SourceKind.Project);
    }

    [Fact]
    public async Task Extra_roots_skip_excluded_folders()
    {
        using var home = new FakeClaudeHome();
        var root = home.Dir("code");
        var vendored = home.Project("code/app/node_modules/pkg");
        home.ProjectSkill(vendored, "x", Doc("x"));

        var result = await SkillScanner.ScanAsync(home.Paths, WithRoots(root));

        result.Sources.Should().NotContain(s => s.Kind == SourceKind.Project);
    }

    [Fact]
    public async Task Extra_roots_do_not_descend_into_a_found_claude_folder()
    {
        using var home = new FakeClaudeHome();
        var root = home.Dir("code");
        var project = home.Project("code/app");
        home.ProjectSkill(project, "x", Doc("x"));
        var worktree = home.Project("code/app/.claude/worktrees/feature");
        home.ProjectSkill(worktree, "y", Doc("y"));

        var result = await SkillScanner.ScanAsync(home.Paths, WithRoots(root));

        result.Sources.Where(s => s.Kind == SourceKind.Project).Select(s => s.DisplayName).Should().Equal("app");
    }

    [Fact]
    public async Task Project_found_by_root_and_by_claude_merges_into_one_source()
    {
        using var home = new FakeClaudeHome();
        var root = home.Dir("code");
        var project = home.Project("code/app");
        home.ProjectSkill(project, "x", Doc("x"));
        home.RegisterProjectFolder(project);

        var result = await SkillScanner.ScanAsync(home.Paths, WithRoots(root));

        result.Sources.Count(s => s.Kind == SourceKind.Project).Should().Be(1);
    }

    [Fact]
    public async Task Missing_extra_root_is_reported_not_thrown()
    {
        using var home = new FakeClaudeHome();
        var missing = Path.Combine(home.Home, "nope");

        var result = await SkillScanner.ScanAsync(home.Paths, WithRoots(missing));

        result.Issues.Should().ContainSingle(i => i.Path == missing);
    }

    [Fact]
    public async Task Project_search_does_not_follow_symlink_loops()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var home = new FakeClaudeHome();
        var root = home.Dir("code");
        var project = home.Project("code/app");
        home.ProjectSkill(project, "x", Doc("x"));
        Directory.CreateSymbolicLink(Path.Combine(project, "loop"), root);

        var result = await SkillScanner.ScanAsync(home.Paths, WithRoots(root) with { SearchDepth = 10 });

        result.Sources.Where(s => s.Kind == SourceKind.Project).Select(s => s.DisplayName).Should().Equal("app");
    }

    [Fact]
    public async Task Duplicate_plugin_keys_produce_one_source()
    {
        using var home = new FakeClaudeHome();
        var install = home.InstallPlugin("m", "p", "1", register: false);
        home.File(Path.Combine(install, "skills", "s", "SKILL.md"), Doc("s"));
        var path = install.Replace("\\", "\\\\");
        home.File(home.Paths.InstalledPluginsJson,
            $$"""{ "plugins": { "p@m": [{ "installPath": "{{path}}" }], "p@m": [{ "installPath": "{{path}}" }] } }""");

        var result = await SkillScanner.ScanAsync(home.Paths);

        result.Sources.Should().ContainSingle(s => s.Kind == SourceKind.Plugin);
        result.Skills.Should().ContainSingle();
    }

    [Fact]
    public async Task Plugin_disabled_in_settings_is_flagged()
    {
        using var home = new FakeClaudeHome();
        home.InstallPlugin("m", "on", "1");
        home.InstallPlugin("m", "off", "1");
        home.EnablePlugins(("on@m", true), ("off@m", false));

        var result = await SkillScanner.ScanAsync(home.Paths);

        result.Sources.Single(s => s.Id == "plugin:m/on").IsEnabled.Should().BeTrue();
        result.Sources.Single(s => s.Id == "plugin:m/off").IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Plugin_absent_from_settings_counts_as_enabled()
    {
        using var home = new FakeClaudeHome();
        home.InstallPlugin("m", "p", "1");

        var result = await SkillScanner.ScanAsync(home.Paths);

        result.Sources.Single(s => s.Kind == SourceKind.Plugin).IsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Unreadable_skills_folder_is_reported_as_issue()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var home = new FakeClaudeHome();
        home.GlobalSkill("x", Doc("x"));
        var skills = Path.Combine(home.ClaudeDir, "skills");
        File.SetUnixFileMode(skills, UnixFileMode.None);
        try
        {
            var result = await SkillScanner.ScanAsync(home.Paths);

            result.Issues.Should().Contain(i => i.Path == skills);
        }
        finally
        {
            File.SetUnixFileMode(skills, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }
}

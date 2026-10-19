using FluentAssertions;
using Lustrum.Core.Copying;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class SkillCopierTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();
    private readonly string _app;

    public SkillCopierTests()
    {
        _app = _home.Project("dev/app");
        Directory.CreateDirectory(Path.Combine(_app, ".claude"));
        _home.RegisterProjectFolder(_app);
        Directory.CreateDirectory(Path.Combine(_home.ClaudeDir, "skills"));
    }

    public void Dispose() => _home.Dispose();

    private static string Doc(string name) => $"---\nname: {name}\ndescription: d\n---\nbody\n";

    private async Task<(ScanResult Scan, SkillSource Global, SkillSource App)> Scan()
    {
        var scan = await SkillScanner.ScanAsync(_home.Paths);
        return (scan, scan.Sources.Single(s => s.Kind == SourceKind.Global), scan.Sources.Single(s => s.Id == "project:" + _app));
    }

    [Fact]
    public async Task Copies_a_personal_skill_folder_into_a_project_without_its_git_folder()
    {
        var file = _home.GlobalSkill("review", Doc("review"));
        var folder = Path.GetDirectoryName(file)!;
        _home.File(Path.Combine(folder, "checklists", "security.md"), "s");
        _home.File(Path.Combine(folder, ".git", "HEAD"), "ref: refs/heads/main");
        var (scan, _, app) = await Scan();
        var skill = scan.Skills.Single(s => s.Name == "review");

        var plan = SkillCopier.Plan(scan, skill, app).Should().BeOfType<ReadyCopy>().Subject;
        var result = SkillCopier.Execute(plan);

        var copy = Path.Combine(_app, ".claude", "skills", "review");
        result.Destination.Should().Be(copy);
        File.ReadAllText(Path.Combine(copy, "SKILL.md")).Should().Be(Doc("review"));
        File.Exists(Path.Combine(copy, "checklists", "security.md")).Should().BeTrue();
        Directory.Exists(Path.Combine(copy, ".git")).Should().BeFalse();
        result.FileCount.Should().Be(2);
        result.Skipped.Should().Equal(".git");
        Directory.GetDirectories(Path.Combine(_app, ".claude", "skills")).Should().ContainSingle();
    }

    [Fact]
    public async Task Linked_files_are_left_out_and_no_staging_folder_remains()
    {
        var file = _home.GlobalSkill("review", Doc("review"));
        var secret = _home.File(Path.Combine(_home.Home, "secret.txt"), "token");
        File.CreateSymbolicLink(Path.Combine(Path.GetDirectoryName(file)!, "notes.txt"), secret);
        var (scan, _, app) = await Scan();

        var result = SkillCopier.Execute((ReadyCopy)SkillCopier.Plan(scan, scan.Skills.Single(), app));

        result.Skipped.Should().Equal("notes.txt");
        File.Exists(Path.Combine(result.Destination, "notes.txt")).Should().BeFalse();
        Directory.GetDirectories(Path.Combine(_app, ".claude")).Select(Path.GetFileName).Should().Equal("skills");
    }

    [Fact]
    public async Task A_folder_over_the_limit_is_refused_without_a_trace()
    {
        var file = _home.GlobalSkill("big", Doc("big"));
        for (var i = 0; i < SkillCopier.MaxFiles + 1; i++)
        {
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(file)!, $"f{i}.txt"), "x");
        }

        var (scan, _, app) = await Scan();

        var execute = () => SkillCopier.Execute((ReadyCopy)SkillCopier.Plan(scan, scan.Skills.Single(), app));

        execute.Should().Throw<SkillCopyException>().Which.Message.Should().StartWith("The skill folder holds more than");
        Directory.Exists(Path.Combine(_app, ".claude", "skills", "big")).Should().BeFalse();
        Directory.GetDirectories(Path.Combine(_app, ".claude")).Should().NotContain(d => Path.GetFileName(d).StartsWith(".lustrum"));
    }

    [Fact]
    public async Task Copies_a_project_command_to_personal_commands_keeping_its_folder()
    {
        _home.File(Path.Combine(_app, ".claude", "commands", "git", "squash.md"), "Squash it");
        var (scan, global, _) = await Scan();

        var plan = (ReadyCopy)SkillCopier.Plan(scan, scan.Skills.Single(s => s.Kind == SkillKind.Command), global);
        SkillCopier.Execute(plan);

        File.ReadAllText(Path.Combine(_home.ClaudeDir, "commands", "git", "squash.md")).Should().Be("Squash it");
    }

    [Fact]
    public async Task Refuses_when_the_folder_already_exists_and_changes_nothing()
    {
        _home.GlobalSkill("review", Doc("review"));
        _home.ProjectSkill(_app, "review", "mine");
        var (scan, _, app) = await Scan();

        var plan = SkillCopier.Plan(scan, scan.Skills.Single(s => s.Source.Kind == SourceKind.Global), app);

        plan.Should().BeOfType<BlockedCopy>().Which.Reason.Should().Be("review already exists in app");
        File.ReadAllText(Path.Combine(_app, ".claude", "skills", "review", "SKILL.md")).Should().Be("mine");
    }

    [Fact]
    public async Task Refuses_when_the_target_has_a_skill_with_the_same_name_in_another_folder()
    {
        _home.ProjectSkill(_app, "code-review", Doc("review"));
        _home.GlobalSkill("review", Doc("review"));
        var (scan, global, _) = await Scan();

        var plan = SkillCopier.Plan(scan, scan.Skills.Single(s => s.Source.Kind == SourceKind.Project), global);

        plan.Should().BeOfType<BlockedCopy>().Which.Reason.Should().Be("your personal skills already has a skill named review");
    }

    [Fact]
    public async Task Execute_refuses_a_destination_that_appeared_after_planning()
    {
        _home.GlobalSkill("review", Doc("review"));
        var (scan, _, app) = await Scan();
        var plan = (ReadyCopy)SkillCopier.Plan(scan, scan.Skills.Single(), app);
        _home.ProjectSkill(_app, "review", "raced");

        var execute = () => SkillCopier.Execute(plan);

        execute.Should().Throw<SkillCopyException>();
        File.ReadAllText(Path.Combine(_app, ".claude", "skills", "review", "SKILL.md")).Should().Be("raced");
    }

    [Fact]
    public async Task Targets_are_personal_skills_and_existing_projects_other_than_the_source()
    {
        _home.GlobalSkill("review", Doc("review"));
        var (scan, _, app) = await Scan();

        SkillCopier.Targets(scan, scan.Skills.Single()).Should().Equal(app);
    }

    [Fact]
    public async Task A_plugin_skill_can_be_copied_out_to_be_customised()
    {
        var plugin = _home.InstallPlugin("m", "kit", "1");
        _home.File(Path.Combine(plugin, "skills", "ui", "SKILL.md"), Doc("ui"));
        var (scan, global, _) = await Scan();

        var plan = (ReadyCopy)SkillCopier.Plan(scan, scan.Skills.Single(), global);
        SkillCopier.Execute(plan);

        File.Exists(Path.Combine(_home.ClaudeDir, "skills", "ui", "SKILL.md")).Should().BeTrue();
        SkillCopier.Targets(scan, scan.Skills.Single()).Select(t => t.Kind).Should().BeEquivalentTo([SourceKind.Global, SourceKind.Project]);
    }
}

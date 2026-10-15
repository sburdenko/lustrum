using FluentAssertions;
using Lustrum.Core.Problems;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class ProblemCollectorTests
{
    [Fact]
    public async Task Collects_broken_skills_long_descriptions_ghost_projects_and_read_issues()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("nofm", "plain text");
        home.GlobalSkill("long", $"---\nname: long\ndescription: {new string('x', 1100)}\n---\n");
        home.GlobalSkill("ok", "---\nname: ok\ndescription: fine\n---\n");
        home.RegisterProjectFolderRaw("-gone-project");
        home.File(home.Paths.InstalledPluginsJson, "{ not json");

        var scan = await SkillScanner.ScanAsync(home.Paths);
        var problems = ProblemCollector.Collect(scan);

        problems.Select(p => p.Kind).Should().Equal(
            ProblemKind.BrokenSkill,
            ProblemKind.LongDescription,
            ProblemKind.MissingProject,
            ProblemKind.ReadIssue);
        problems[0].Skill!.Name.Should().Be("nofm");
        problems[2].Subject.Should().Contain("gone");
        problems[3].Path.Should().Be(home.Paths.InstalledPluginsJson);
    }

    [Fact]
    public async Task Yaml_error_detail_is_attached_to_the_broken_skill_and_not_repeated_as_read_issue()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("bad", "---\nname: [oops\n---\n");

        var scan = await SkillScanner.ScanAsync(home.Paths);
        var problems = ProblemCollector.Collect(scan);

        var problem = problems.Should().ContainSingle().Subject;
        problem.Kind.Should().Be(ProblemKind.BrokenSkill);
        problem.Detail.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Healthy_home_has_no_problems()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("ok", "---\nname: ok\ndescription: fine\n---\n");

        var scan = await SkillScanner.ScanAsync(home.Paths);

        ProblemCollector.Collect(scan).Should().BeEmpty();
    }
}

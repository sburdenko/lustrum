using FluentAssertions;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;
using Lustrum.Core.Watching;

namespace Lustrum.Core.Tests;

public class WatchTargetsTests
{
    [Fact]
    public async Task Watches_skill_folders_recursively_but_busy_claude_folders_shallowly()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("g", "---\nname: g\ndescription: d\n---\n");
        var project = home.Project("dev/app");
        home.ProjectSkill(project, "p", "---\nname: p\ndescription: d\n---\n");
        home.RegisterProjectFolder(project);
        var scan = await SkillScanner.ScanAsync(home.Paths);

        var targets = WatchTargets.From(scan, home.Paths);

        targets.Should().Contain(new WatchTarget(home.Paths.SkillsDir, Recursive: true));
        targets.Should().Contain(new WatchTarget(Path.Combine(project, ".claude", "skills"), Recursive: true));
        targets.Should().Contain(new WatchTarget(Path.Combine(project, ".claude"), Recursive: false));
        targets.Should().Contain(new WatchTarget(home.Paths.ProjectsDir, Recursive: false));
        targets.Should().NotContain(t => t.Path == home.ClaudeDir && t.Filter == WatchTarget.All);
        targets.Where(t => t.Path == home.ClaudeDir).Select(t => t.Filter)
            .Should().BeEquivalentTo("settings.json", "skills", "commands");
        targets.Should().NotContain(t => t.Path == home.Paths.ProjectsDir && t.Recursive);
    }
}

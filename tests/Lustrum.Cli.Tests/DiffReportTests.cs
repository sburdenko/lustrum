using FluentAssertions;
using Lustrum.Core.Models;

namespace Lustrum.Cli.Tests;

public class DiffReportTests
{
    private static readonly SkillSource Global = new("global", SourceKind.Global, "Global", "/h/.claude", null, true);
    private static readonly SkillSource App = new("project:/dev/app", SourceKind.Project, "app", "/dev/app/.claude", null, true);

    private static Skill Skill(SkillSource source, string name, string path) => new(
        $"{source.Id}#{name}", name, name, "d", SkillKind.Skill, source, path,
        null, new Dictionary<string, string>(), [], [], 1, DateTimeOffset.UnixEpoch, Health.Ok, []);

    private static readonly Dictionary<string, string> Files = new()
    {
        ["/g/SKILL.md"] = "same\nglobal line\n",
        ["/a/SKILL.md"] = "same\nproject line\n",
    };

    [Fact]
    public void Project_copy_is_the_base_and_every_other_copy_is_compared_with_it()
    {
        var report = DiffReport.Render([Skill(Global, "review", "/g/SKILL.md"), Skill(App, "review", "/a/SKILL.md")], p => Files[p]);

        report.Should().StartWith("--- app: /a/SKILL.md\n+++ Global: /g/SKILL.md\n");
        report.Should().Contain("-project line").And.Contain("+global line");
    }

    [Fact]
    public void A_single_copy_has_nothing_to_compare()
    {
        DiffReport.Render([Skill(Global, "review", "/g/SKILL.md")], p => Files[p])
            .Should().Be("Only one copy of 'review': /g/SKILL.md");
    }

    [Fact]
    public void Unreadable_copy_is_reported_in_place()
    {
        var report = DiffReport.Render(
            [Skill(App, "review", "/a/SKILL.md"), Skill(Global, "review", "/missing/SKILL.md")],
            p => Files.TryGetValue(p, out var text) ? text : throw new FileNotFoundException("gone"));

        report.Should().Contain("Could not read /missing/SKILL.md: gone");
    }
}

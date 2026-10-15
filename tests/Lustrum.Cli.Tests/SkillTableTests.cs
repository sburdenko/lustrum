using FluentAssertions;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;

namespace Lustrum.Cli.Tests;

public class SkillTableTests
{
    private static readonly SkillSource Global = new("global", SourceKind.Global, "Global", "/h/.claude", null, true);
    private static readonly SkillSource App = new("project:/dev/app", SourceKind.Project, "app", "/dev/app/.claude", null, true);

    private static Skill Skill(SkillSource source, string name) => new(
        $"{source.Id}#{name}", name, name, $"{name} description", SkillKind.Skill, source, $"/x/{name}/SKILL.md",
        null, new Dictionary<string, string>(), [], [], 1, DateTimeOffset.UnixEpoch, Health.Ok, []);

    [Fact]
    public void Plain_table_has_no_project_column()
    {
        var table = SkillTable.Render([Skill(Global, "review")]);

        table.Should().StartWith("NAME").And.NotContain("IN PROJECT");
    }

    [Fact]
    public void Effective_table_shows_what_wins_in_the_project()
    {
        var global = Skill(Global, "review");
        var local = Skill(App, "review");
        var entries = new[]
        {
            new EffectiveEntry(local, Availability.Active, null, [global]),
            new EffectiveEntry(global, Availability.Shadowed, local, []),
            new EffectiveEntry(Skill(Global, "solo"), Availability.Active, null, []),
        };

        var lines = SkillTable.Render(entries).Split('\n');

        lines[0].Should().Contain("IN PROJECT");
        lines[1].Should().Contain("overrides Global");
        lines[2].Should().Contain("shadowed by app");
        lines[3].Should().Contain("loaded");
    }

    [Fact]
    public void Empty_list_says_so()
    {
        SkillTable.Render(Array.Empty<Skill>()).Should().Be("No skills found.");
        SkillTable.Render(Array.Empty<EffectiveEntry>()).Should().Be("No skills found.");
    }
}

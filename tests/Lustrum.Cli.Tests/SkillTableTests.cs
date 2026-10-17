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

    [Fact]
    public void Usage_columns_show_count_and_last_use()
    {
        var report = Lustrum.Core.Usage.UsageReport.Empty with
        {
            ByName = new Dictionary<string, Lustrum.Core.Usage.SkillUsage>
            {
                ["review"] = new("review", 3, DateTimeOffset.Parse("2026-10-01T10:00:00Z"), [], 2, 1),
            },
        };

        var lines = SkillTable.Render([Skill(Global, "review"), Skill(Global, "solo")], report).Split('\n');

        lines[0].Should().Contain("USES").And.Contain("LAST USED");
        lines[1].Should().Contain("3").And.Contain("2026-10-01");
        lines[2].Should().Contain("0").And.Contain("never");
    }

    [Fact]
    public void Effective_table_can_show_usage_too()
    {
        var skill = Skill(App, "review");
        var report = Lustrum.Core.Usage.UsageReport.Empty with
        {
            ByName = new Dictionary<string, Lustrum.Core.Usage.SkillUsage>
            {
                ["review"] = new("review", 2, DateTimeOffset.Parse("2026-10-02T10:00:00Z"), [], 2, 0),
            },
        };

        var lines = SkillTable.Render([new EffectiveEntry(skill, Availability.Active, null, [])], report).Split('\n');

        lines[0].Should().Contain("IN PROJECT").And.Contain("USES");
        lines[1].Should().Contain("loaded").And.Contain("2026-10-02");
    }

    [Fact]
    public void Missing_table_says_where_each_skill_lives()
    {
        var other = new SkillSource("project:/dev/other", SourceKind.Project, "other", "/dev/other/.claude", null, true);
        var entries = new[] { new MissingEntry(Skill(other, "theirs"), null), new MissingEntry(Skill(other, "review"), Skill(Global, "review")) };

        var lines = SkillTable.Render(entries).Split('\n');

        lines[0].Should().Contain("NOTE");
        lines[1].Should().NotContain("loaded here");
        lines[2].Should().Contain("loaded here from Global");
    }
}

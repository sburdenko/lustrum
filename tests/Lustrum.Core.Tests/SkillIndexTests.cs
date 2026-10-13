using FluentAssertions;
using Lustrum.Core.Indexing;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class SkillIndexTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();
    private readonly string _project;

    public SkillIndexTests()
    {
        _project = _home.Project("dev/app");
        _home.RegisterProjectFolder(_project);
        _home.GlobalSkill("design", "---\nname: design\ndescription: Visual design and branding\n---\n");
        _home.GlobalSkill("sb-notes", "---\nname: sb-notes\ndescription: Personal notes\n---\n");
        _home.GlobalSkill("broken", "no frontmatter at all");
        _home.ProjectSkill(_project, "local", "---\nname: local\ndescription: Project only helper\n---\n");
        var plugin = _home.InstallPlugin("m", "unity", "1");
        _home.File(Path.Combine(plugin, "skills", "ui", "SKILL.md"), "---\nname: ui\ndescription: Unity UI toolkit\n---\n");
    }

    public void Dispose() => _home.Dispose();

    private async Task<IReadOnlyList<Skill>> Query(SkillScope scope, string text = "")
    {
        var scan = await SkillScanner.ScanAsync(_home.Paths);
        return SkillIndex.Search(scan.Skills, new SkillQuery(scope, text));
    }

    [Fact]
    public async Task All_scope_returns_everything()
    {
        var skills = await Query(SkillScope.All);

        skills.Should().HaveCount(5);
    }

    [Fact]
    public async Task Kind_scope_filters_by_source_kind()
    {
        var skills = await Query(new KindScope(SourceKind.Global));

        skills.Select(s => s.Name).Should().BeEquivalentTo("design", "sb-notes", "broken");
    }

    [Fact]
    public async Task Source_scope_filters_by_exact_source()
    {
        var skills = await Query(new SourceScope("project:" + _project));

        skills.Select(s => s.Name).Should().Equal("local");
    }

    [Fact]
    public async Task Marketplace_scope_includes_all_plugins_of_that_marketplace()
    {
        var other = _home.InstallPlugin("elsewhere", "tool", "1");
        _home.File(Path.Combine(other, "skills", "x", "SKILL.md"), "---\nname: x\ndescription: d\n---\n");

        var skills = await Query(new MarketplaceScope("m"));

        skills.Select(s => s.FullName).Should().Equal("unity:ui");
    }

    [Fact]
    public async Task Problems_scope_returns_only_broken()
    {
        var skills = await Query(SkillScope.Problems);

        skills.Select(s => s.Name).Should().Equal("broken");
    }

    [Fact]
    public async Task Text_matches_name_and_description_case_insensitively()
    {
        (await Query(SkillScope.All, "BRAND")).Select(s => s.Name).Should().Equal("design");
        (await Query(SkillScope.All, "unity")).Select(s => s.FullName).Should().Equal("unity:ui");
    }

    [Fact]
    public async Task Every_word_must_match()
    {
        (await Query(SkillScope.All, "personal notes")).Select(s => s.Name).Should().Equal("sb-notes");
        (await Query(SkillScope.All, "personal design")).Should().BeEmpty();
    }

    [Fact]
    public async Task Tag_words_are_searchable()
    {
        (await Query(SkillScope.All, "broken")).Select(s => s.Name).Should().Contain("broken");
    }
}

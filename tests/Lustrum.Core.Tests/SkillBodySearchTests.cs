using FluentAssertions;
using Lustrum.Core.Indexing;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class SkillBodySearchTests
{
    [Fact]
    public async Task Finds_skills_whose_body_contains_every_word()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("a", "---\nname: a\ndescription: d\n---\nRun the Migration script, then verify.\n");
        home.GlobalSkill("b", "---\nname: b\ndescription: migration\n---\nNothing relevant.\n");
        home.GlobalSkill("c", "---\nname: c\ndescription: d\n---\nmigration only\n");
        var scan = await SkillScanner.ScanAsync(home.Paths);

        var ids = await SkillBodySearch.FindAsync(scan.Skills, "migration verify", CancellationToken.None);

        scan.Skills.Where(s => ids.Contains(s.Id)).Select(s => s.Name).Should().Equal("a");
    }

    [Fact]
    public async Task Empty_text_matches_nothing()
    {
        using var home = new FakeClaudeHome();
        home.GlobalSkill("a", "---\nname: a\ndescription: d\n---\nbody\n");
        var scan = await SkillScanner.ScanAsync(home.Paths);

        (await SkillBodySearch.FindAsync(scan.Skills, "  ", CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task Missing_files_are_skipped()
    {
        using var home = new FakeClaudeHome();
        var path = home.GlobalSkill("a", "---\nname: a\ndescription: d\n---\nneedle\n");
        var scan = await SkillScanner.ScanAsync(home.Paths);
        File.Delete(path);

        (await SkillBodySearch.FindAsync(scan.Skills, "needle", CancellationToken.None)).Should().BeEmpty();
    }
}

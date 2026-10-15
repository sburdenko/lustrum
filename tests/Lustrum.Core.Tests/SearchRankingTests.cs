using FluentAssertions;
using Lustrum.Core.Indexing;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class SearchRankingTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();

    public SearchRankingTests()
    {
        _home.GlobalSkill("release-notes", "---\nname: release-notes\ndescription: Draft notes\n---\n");
        _home.GlobalSkill("notes", "---\nname: notes\ndescription: Daily log\n---\n");
        _home.GlobalSkill("sb-mine", "---\nname: sb-mine\ndescription: Mentions release in passing\n---\n");
        _home.GlobalCommand("deploy", "Ship it");
    }

    public void Dispose() => _home.Dispose();

    private async Task<IReadOnlyList<string>> Names(string text, params string[] tags)
    {
        var scan = await SkillScanner.ScanAsync(_home.Paths);
        return SkillIndex.Search(scan.Skills, new SkillQuery(SkillScope.All, text, tags)).Select(s => s.Name).ToList();
    }

    [Fact]
    public async Task Results_are_ordered_by_relevance()
    {
        (await Names("notes")).Should().Equal("notes", "release-notes");
    }

    [Fact]
    public async Task Name_matches_rank_above_description_matches()
    {
        (await Names("release")).Should().Equal("release-notes", "sb-mine");
    }

    [Fact]
    public async Task Fuzzy_name_match_is_found()
    {
        (await Names("rlsnotes")).Should().Equal("release-notes");
    }

    [Fact]
    public async Task Descriptions_are_not_matched_as_scattered_subsequences()
    {
        (await Names("dlylg")).Should().BeEmpty();
    }

    [Fact]
    public async Task Without_text_results_keep_scan_order()
    {
        (await Names("")).Should().Equal("deploy", "notes", "release-notes", "sb-mine");
    }

    [Fact]
    public async Task Required_tags_must_all_be_present()
    {
        (await Names("", "sb-")).Should().Equal("sb-mine");
        (await Names("", "command")).Should().Equal("deploy");
        (await Names("", "command", "sb-")).Should().BeEmpty();
    }
}

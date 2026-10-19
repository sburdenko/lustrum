using FluentAssertions;
using Lustrum.Core.Git;

namespace Lustrum.Core.Tests;

public class GitStatusParserTests
{
    [Fact]
    public void Parses_each_kind_of_change()
    {
        var output = " M skills/a/SKILL.md\0A  skills/b/SKILL.md\0 D skills/c/SKILL.md\0?? skills/d/notes.md\0UU skills/e/SKILL.md\0";

        GitStatusParser.Parse(output).Should().Equal(
            new GitChange("skills/a/SKILL.md", GitChangeKind.Modified),
            new GitChange("skills/b/SKILL.md", GitChangeKind.Added),
            new GitChange("skills/c/SKILL.md", GitChangeKind.Deleted),
            new GitChange("skills/d/notes.md", GitChangeKind.Untracked),
            new GitChange("skills/e/SKILL.md", GitChangeKind.Conflicted));
    }

    [Fact]
    public void A_rename_consumes_the_old_path_that_follows_it()
    {
        var output = "R  new name/SKILL.md\0old name/SKILL.md\0 M other.md\0";

        GitStatusParser.Parse(output).Should().Equal(
            new GitChange("new name/SKILL.md", GitChangeKind.Renamed),
            new GitChange("other.md", GitChangeKind.Modified));
    }

    [Fact]
    public void Empty_output_means_a_clean_tree() => GitStatusParser.Parse(string.Empty).Should().BeEmpty();
}

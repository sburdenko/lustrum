using FluentAssertions;
using Lustrum.Core.Comparing;

namespace Lustrum.Core.Tests;

public class SkillComparerTests
{
    [Fact]
    public void Identical_text_has_no_changes()
    {
        var diff = SkillComparer.Compare("a\nb\n", "a\nb\n");

        diff.IsIdentical.Should().BeTrue();
        diff.Added.Should().Be(0);
        diff.Removed.Should().Be(0);
    }

    [Fact]
    public void Line_endings_do_not_count_as_changes()
    {
        SkillComparer.Compare("a\r\nb\r\n", "a\nb\n").IsIdentical.Should().BeTrue();
    }

    [Fact]
    public void Added_and_removed_lines_are_numbered_on_their_own_side()
    {
        var diff = SkillComparer.Compare("one\ntwo\nthree", "one\n2\nthree\nfour");

        diff.Added.Should().Be(2);
        diff.Removed.Should().Be(1);
        diff.Lines.Should().ContainEquivalentOf(new DiffLine(DiffKind.Removed, "two", 2, null));
        diff.Lines.Should().ContainEquivalentOf(new DiffLine(DiffKind.Added, "2", null, 2));
        diff.Lines.Should().ContainEquivalentOf(new DiffLine(DiffKind.Same, "three", 3, 3));
        diff.Lines.Last().Should().Be(new DiffLine(DiffKind.Added, "four", null, 4));
    }

    [Fact]
    public void Long_unchanged_runs_are_folded_around_the_changes()
    {
        var left = string.Join('\n', Enumerable.Range(1, 30).Select(i => $"line {i}"));
        var right = left.Replace("line 15", "changed");

        var rows = DiffFolding.Fold(SkillComparer.Compare(left, right).Lines, context: 2);

        rows.OfType<DiffGap>().Select(g => g.Hidden).Should().Equal(12, 13);
        rows.OfType<DiffLine>().Select(l => l.Text).Should().Equal("line 13", "line 14", "line 15", "changed", "line 16", "line 17");
    }

    [Fact]
    public void Short_unchanged_runs_are_not_folded()
    {
        var rows = DiffFolding.Fold(SkillComparer.Compare("a\nb\nc", "a\nB\nc").Lines, context: 3);

        rows.Should().NotContain(r => r is DiffGap);
    }

    [Fact]
    public void Unified_format_has_headers_and_hunk_markers()
    {
        var diff = SkillComparer.Compare("a\nb\nc", "a\nB\nc");

        var text = UnifiedDiff.Format(diff, "Global: /x/SKILL.md", "app: /y/SKILL.md");

        text.Split('\n').Should().StartWith(new[] { "--- Global: /x/SKILL.md", "+++ app: /y/SKILL.md", "@@ -1,3 +1,3 @@" });
        text.Should().Contain("\n-b\n").And.Contain("\n+B\n").And.Contain("\n a\n");
    }

    [Fact]
    public void Unified_format_says_when_files_are_identical()
    {
        UnifiedDiff.Format(SkillComparer.Compare("a", "a"), "l", "r").Should().Be("Files are identical.");
    }
}

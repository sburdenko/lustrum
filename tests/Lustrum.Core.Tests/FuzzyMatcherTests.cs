using FluentAssertions;
using Lustrum.Core.Indexing;

namespace Lustrum.Core.Tests;

public class FuzzyMatcherTests
{
    [Theory]
    [InlineData("release-notes", "release-notes")]
    [InlineData("release-notes", "rel")]
    [InlineData("release-notes", "notes")]
    [InlineData("release-notes", "rlsnotes")]
    [InlineData("Release-Notes", "RELNOT")]
    public void Matches(string candidate, string pattern) =>
        FuzzyMatcher.Score(candidate, pattern).Should().BeGreaterThan(0);

    [Theory]
    [InlineData("release-notes", "xyz")]
    [InlineData("release-notes", "seton")]
    [InlineData("code-review", "")]
    public void Does_not_match(string candidate, string pattern) =>
        FuzzyMatcher.Score(candidate, pattern).Should().Be(0);

    [Fact]
    public void Short_patterns_do_not_match_as_scattered_subsequences()
    {
        FuzzyMatcher.Score("release-notes", "rn").Should().Be(0);
    }

    [Fact]
    public void Exact_beats_prefix_beats_word_start_beats_substring_beats_subsequence()
    {
        var exact = FuzzyMatcher.Score("notes", "notes");
        var prefix = FuzzyMatcher.Score("notes-app", "notes");
        var wordStart = FuzzyMatcher.Score("release-notes", "notes");
        var substring = FuzzyMatcher.Score("footnotes", "notes");
        var subsequence = FuzzyMatcher.Score("n-o-t-e-s", "notes");

        exact.Should().BeGreaterThan(prefix);
        prefix.Should().BeGreaterThan(wordStart);
        wordStart.Should().BeGreaterThan(substring);
        substring.Should().BeGreaterThan(subsequence);
        subsequence.Should().BeGreaterThan(0);
    }
}

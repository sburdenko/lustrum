namespace Lustrum.Core.Indexing;

/// <summary>
/// Scores how well a pattern matches a candidate: exact, prefix, word start, substring, then an ordered
/// subsequence. Zero means no match. Subsequences need at least three characters and a compact span,
/// otherwise short patterns would match almost anything.
/// </summary>
public static class FuzzyMatcher
{
    public const int Exact = 1000;
    public const int Prefix = 800;
    public const int WordStart = 600;
    public const int Substring = 400;
    private const int SubsequenceBase = 200;
    private const int MinSubsequenceLength = 3;
    private const int MaxSpanFactor = 3;

    public static int Score(string candidate, string pattern)
    {
        if (pattern.Length == 0 || candidate.Length == 0)
        {
            return 0;
        }

        var index = candidate.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
        if (index == 0)
        {
            return candidate.Length == pattern.Length ? Exact : Prefix;
        }

        if (index > 0)
        {
            return IsWordStart(candidate, index) ? WordStart : Substring;
        }

        return pattern.Length < MinSubsequenceLength ? 0 : SubsequenceScore(candidate, pattern);
    }

    private static int SubsequenceScore(string candidate, string pattern)
    {
        var first = -1;
        var position = 0;
        foreach (var ch in pattern)
        {
            position = IndexOfIgnoreCase(candidate, ch, position);
            if (position < 0)
            {
                return 0;
            }

            first = first < 0 ? position : first;
            position++;
        }

        var span = position - first;
        if (span > pattern.Length * MaxSpanFactor)
        {
            return 0;
        }

        return Math.Max(1, SubsequenceBase - (span - pattern.Length));
    }

    private static int IndexOfIgnoreCase(string text, char ch, int start)
    {
        var lower = char.ToLowerInvariant(ch);
        for (var i = start; i < text.Length; i++)
        {
            if (char.ToLowerInvariant(text[i]) == lower)
            {
                return i;
            }
        }

        return -1;
    }

    private static bool IsWordStart(string text, int index) =>
        !char.IsLetterOrDigit(text[index - 1]) || (char.IsLower(text[index - 1]) && char.IsUpper(text[index]));
}

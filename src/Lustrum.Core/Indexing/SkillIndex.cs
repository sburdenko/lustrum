using Lustrum.Core.Models;

namespace Lustrum.Core.Indexing;

/// <summary>
/// Filters by scope and required tags, then by text. Every word must match; names are matched fuzzily,
/// descriptions and tags only as substrings. With text, results are ordered by relevance.
/// </summary>
public static class SkillIndex
{
    private const int DescriptionScore = 100;
    private const int TagScore = 150;

    public static IReadOnlyList<Skill> Search(IReadOnlyList<Skill> skills, SkillQuery query)
    {
        var tags = query.RequiredTags ?? [];
        var filtered = skills
            .Where(query.Scope.Includes)
            .Where(s => tags.All(s.Tags.Contains));

        var words = query.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
        {
            return filtered.ToList();
        }

        return filtered
            .Select(skill => (Skill: skill, Score: Score(skill, words)))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .Select(r => r.Skill)
            .ToList();
    }

    private static int Score(Skill skill, IEnumerable<string> words)
    {
        var total = 0;
        foreach (var word in words)
        {
            var score = WordScore(skill, word);
            if (score == 0)
            {
                return 0;
            }

            total += score;
        }

        return total;
    }

    private static int WordScore(Skill skill, string word)
    {
        var name = Math.Max(FuzzyMatcher.Score(skill.Name, word), FuzzyMatcher.Score(skill.FullName, word));
        var description = skill.Description?.Contains(word, StringComparison.OrdinalIgnoreCase) == true ? DescriptionScore : 0;
        var tag = skill.Tags.Any(t => t.Contains(word, StringComparison.OrdinalIgnoreCase)) ? TagScore : 0;
        return Math.Max(name, Math.Max(description, tag));
    }
}

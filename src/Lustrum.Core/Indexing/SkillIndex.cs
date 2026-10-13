using Lustrum.Core.Models;

namespace Lustrum.Core.Indexing;

public static class SkillIndex
{
    public static IReadOnlyList<Skill> Search(IReadOnlyList<Skill> skills, SkillQuery query)
    {
        var words = query.Text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return skills
            .Where(query.Scope.Includes)
            .Where(skill => words.All(word => Haystack(skill).Contains(word, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    private static string Haystack(Skill skill) =>
        string.Join(' ', skill.Name, skill.FullName, skill.Description ?? string.Empty, string.Join(' ', skill.Tags));
}

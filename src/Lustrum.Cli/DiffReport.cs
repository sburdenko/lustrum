using Lustrum.Core.Comparing;
using Lustrum.Core.Models;

namespace Lustrum.Cli;

internal static class DiffReport
{
    public static string Render(IReadOnlyList<Skill> copies, Func<string, string> readText)
    {
        var ordered = copies.OrderBy(c => c.Source.Kind == SourceKind.Project ? 0 : 1).ThenBy(c => c.Source.Kind).ToList();
        var baseCopy = ordered[0];
        if (ordered.Count == 1)
        {
            return $"Only one copy of '{baseCopy.FullName}': {baseCopy.Path}";
        }

        if (!TryRead(baseCopy, readText, out var baseText))
        {
            return baseText;
        }

        var sections = ordered.Skip(1).Select(other => TryRead(other, readText, out var otherText)
            ? UnifiedDiff.Format(SkillComparer.Compare(baseText, otherText), Label(baseCopy), Label(other))
            : otherText);
        return string.Join("\n\n", sections);
    }

    private static bool TryRead(Skill skill, Func<string, string> readText, out string text)
    {
        try
        {
            text = readText(skill.Path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            text = $"Could not read {skill.Path}: {ex.Message}";
            return false;
        }
    }

    private static string Label(Skill skill) => $"{skill.Source.DisplayName}: {skill.Path}";
}

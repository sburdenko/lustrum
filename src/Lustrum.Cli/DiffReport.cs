using Lustrum.Core.Comparing;
using Lustrum.Core.Models;

namespace Lustrum.Cli;

internal sealed record DiffReportResult(string Text, IReadOnlyList<string> Errors)
{
    public bool Failed => Errors.Count > 0;
}

internal static class DiffReport
{
    public static DiffReportResult Render(IReadOnlyList<Skill> copies, Func<string, string> readText)
    {
        var ordered = copies.OrderBy(c => c.Source.Kind == SourceKind.Project ? 0 : 1).ThenBy(c => c.Source.Kind).ToList();
        var baseCopy = ordered[0];
        if (ordered.Count == 1)
        {
            return new DiffReportResult($"Only one copy of '{baseCopy.FullName}': {baseCopy.Path}", []);
        }

        var errors = new List<string>();
        if (!TryRead(baseCopy, readText, errors, out var baseText))
        {
            return new DiffReportResult(string.Empty, errors);
        }

        var sections = new List<string>();
        foreach (var other in ordered.Skip(1))
        {
            if (TryRead(other, readText, errors, out var otherText))
            {
                sections.Add(UnifiedDiff.Format(SkillComparer.Compare(baseText, otherText), Label(baseCopy), Label(other)));
            }
        }

        return new DiffReportResult(string.Join("\n\n", sections), errors);
    }

    private static bool TryRead(Skill skill, Func<string, string> readText, List<string> errors, out string text)
    {
        try
        {
            text = readText(skill.Path);
            return true;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            errors.Add($"Could not read {skill.Path}: {ex.Message}");
            text = string.Empty;
            return false;
        }
    }

    private static string Label(Skill skill) => $"{skill.Source.DisplayName}: {skill.Path}";
}

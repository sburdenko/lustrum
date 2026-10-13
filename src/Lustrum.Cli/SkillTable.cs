using System.Text;
using Lustrum.Core.Models;

namespace Lustrum.Cli;

internal static class SkillTable
{
    private const int DescriptionWidth = 72;

    public static string Render(IReadOnlyList<Skill> skills)
    {
        if (skills.Count == 0)
        {
            return "No skills found.";
        }

        var nameWidth = Math.Max("NAME".Length, skills.Max(s => s.InvocationName.Length));
        var sourceWidth = Math.Max("SOURCE".Length, skills.Max(s => s.Source.DisplayName.Length));
        var builder = new StringBuilder();
        builder.AppendLine(Row("NAME", "SOURCE", "HEALTH", "DESCRIPTION", nameWidth, sourceWidth));
        foreach (var skill in skills)
        {
            builder.AppendLine(Row(
                skill.InvocationName,
                skill.Source.DisplayName,
                skill.Health == Health.Ok ? "ok" : skill.Health.ToString(),
                Truncate(skill.Description ?? "(no description)"),
                nameWidth,
                sourceWidth));
        }

        return builder.ToString().TrimEnd();
    }

    private static string Row(string name, string source, string health, string description, int nameWidth, int sourceWidth) =>
        $"{name.PadRight(nameWidth)}  {source.PadRight(sourceWidth)}  {health.PadRight(18)}  {description}";

    private static string Truncate(string text)
    {
        var single = text.ReplaceLineEndings(" ");
        return single.Length <= DescriptionWidth ? single : single[..(DescriptionWidth - 1)] + "…";
    }
}

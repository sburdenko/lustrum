using System.Text;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;

namespace Lustrum.Cli;

internal static class SkillTable
{
    private const int DescriptionWidth = 72;
    private const int HealthWidth = 18;
    private const string Empty = "No skills found.";

    public static string Render(IReadOnlyList<Skill> skills) =>
        skills.Count == 0 ? Empty : Build(skills.Select(s => (s, (string?)null)).ToList(), withProject: false);

    public static string Render(IReadOnlyList<EffectiveEntry> entries) =>
        entries.Count == 0
            ? Empty
            : Build(entries.Select(e => (e.Skill, (string?)(e.Label(s => s.DisplayName) ?? "loaded"))).ToList(), withProject: true);

    private static string Build(IReadOnlyList<(Skill Skill, string? InProject)> rows, bool withProject)
    {
        var nameWidth = Math.Max("NAME".Length, rows.Max(r => r.Skill.InvocationName.Length));
        var sourceWidth = Math.Max("SOURCE".Length, rows.Max(r => r.Skill.Source.DisplayName.Length));
        var projectWidth = withProject ? Math.Max("IN PROJECT".Length, rows.Max(r => r.InProject!.Length)) : 0;

        var builder = new StringBuilder();
        builder.AppendLine(Row("NAME", "SOURCE", "HEALTH", withProject ? "IN PROJECT" : null, "DESCRIPTION", nameWidth, sourceWidth, projectWidth));
        foreach (var (skill, inProject) in rows)
        {
            builder.AppendLine(Row(
                skill.InvocationName,
                skill.Source.DisplayName,
                skill.Health == Health.Ok ? "ok" : skill.Health.ToString(),
                inProject,
                Truncate(skill.Description ?? "(no description)"),
                nameWidth,
                sourceWidth,
                projectWidth));
        }

        return builder.ToString().TrimEnd();
    }

    private static string Row(string name, string source, string health, string? inProject, string description, int nameWidth, int sourceWidth, int projectWidth)
    {
        var project = inProject is null ? string.Empty : inProject.PadRight(projectWidth) + "  ";
        return $"{name.PadRight(nameWidth)}  {source.PadRight(sourceWidth)}  {health.PadRight(HealthWidth)}  {project}{description}";
    }

    private static string Truncate(string text)
    {
        var single = text.ReplaceLineEndings(" ");
        return single.Length <= DescriptionWidth ? single : single[..(DescriptionWidth - 1)] + "…";
    }
}

using System.Globalization;
using System.Text;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;
using Lustrum.Core.Usage;

namespace Lustrum.Cli;

internal static class SkillTable
{
    private const int DescriptionWidth = 72;
    private const string Empty = "No skills found.";

    private static readonly Column[] Base =
    [
        new("NAME", r => r.Skill.InvocationName),
        new("SOURCE", r => r.Skill.Source.DisplayName),
        new("HEALTH", r => r.Skill.Health == Health.Ok ? "ok" : r.Skill.Health.ToString()),
    ];

    public static string Render(IReadOnlyList<Skill> skills) =>
        Build(skills.Select(s => new Row(s, null, null)).ToList(), Base);

    public static string Render(IReadOnlyList<EffectiveEntry> entries) =>
        Build(
            entries.Select(e => new Row(e.Skill, e, null)).ToList(),
            [.. Base, InProject]);

    private static readonly Column InProject = new("IN PROJECT", r => r.Entry!.Label(s => s.DisplayName) ?? "loaded");

    private static readonly Column WhereMissing = new("NOTE", r => r.Missing!.Reason(s => s.DisplayName) ?? string.Empty);

    private static readonly Column[] UsageColumns =
    [
        new("USES", r => (r.Usage?.Count ?? 0).ToString(CultureInfo.InvariantCulture)),
        new("LAST USED", r => r.Usage?.LastUsed.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "never"),
    ];

    public static string Render(IReadOnlyList<MissingEntry> entries) =>
        Build(entries.Select(e => new Row(e.Skill, null, null, e)).ToList(), [.. Base, WhereMissing]);

    public static string Render(IReadOnlyList<MissingEntry> entries, UsageReport usage) =>
        Build(entries.Select(e => new Row(e.Skill, null, usage.For(e.Skill.FullName), e)).ToList(), [.. Base, WhereMissing, .. UsageColumns]);

    public static string Render(IReadOnlyList<Skill> skills, UsageReport usage) =>
        Build(skills.Select(s => new Row(s, null, usage.For(s.FullName))).ToList(), [.. Base, .. UsageColumns]);

    public static string Render(IReadOnlyList<EffectiveEntry> entries, UsageReport usage) =>
        Build(entries.Select(e => new Row(e.Skill, e, usage.For(e.Skill.FullName))).ToList(), [.. Base, InProject, .. UsageColumns]);

    private static string Build(IReadOnlyList<Row> rows, IReadOnlyList<Column> columns)
    {
        if (rows.Count == 0)
        {
            return Empty;
        }

        var widths = columns.Select(c => Math.Max(c.Header.Length, rows.Max(r => c.Value(r).Length))).ToList();
        var builder = new StringBuilder();
        builder.AppendLine(Line(columns.Select(c => c.Header).ToList(), widths, "DESCRIPTION"));
        foreach (var row in rows)
        {
            builder.AppendLine(Line(columns.Select(c => c.Value(row)).ToList(), widths, Truncate(row.Skill.Description ?? "(no description)")));
        }

        return builder.ToString().TrimEnd();
    }

    private static string Line(IReadOnlyList<string> cells, IReadOnlyList<int> widths, string description) =>
        string.Concat(cells.Select((cell, i) => cell.PadRight(widths[i]) + "  ")) + description;

    private static string Truncate(string text)
    {
        var single = text.ReplaceLineEndings(" ");
        return single.Length <= DescriptionWidth ? single : single[..(DescriptionWidth - 1)] + "…";
    }

    private sealed record Row(Skill Skill, EffectiveEntry? Entry, SkillUsage? Usage, MissingEntry? Missing = null);

    private sealed record Column(string Header, Func<Row, string> Value);
}

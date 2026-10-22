using System.Globalization;
using System.Text;
using Lustrum.Core.Budget;
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

    public static string RenderBudget(IReadOnlyList<EffectiveEntry> entries) =>
        Build(
            entries.OrderByDescending(ContextBudget.ListedChars).ThenBy(e => e.Skill.FullName, StringComparer.OrdinalIgnoreCase).Select(e => new Row(e.Skill, e, null)).ToList(),
            [.. Base, InProject, new("≈TOKENS", r => ContextBudget.Tokens(ContextBudget.ListedChars(r.Entry!)).ToString(CultureInfo.InvariantCulture))]);

    public static string BudgetLine(BudgetSummary budget)
    {
        var line = $"≈{budget.ListedTokens.ToString("N0", CultureInfo.InvariantCulture)} tokens in Claude's skill list from {budget.ListedSkills} listed skills";
        var hidden = budget.HiddenSkills > 0 ? $", {budget.HiddenSkills} hidden" : string.Empty;
        var onDemand = budget.OnDemandSkills > 0
            ? $"; {budget.OnDemandSkills} more on demand, ≈{budget.OnDemandTokens.ToString("N0", CultureInfo.InvariantCulture)} tokens"
            : string.Empty;
        return $"{line}{hidden}{onDemand}. Estimate: about four characters per token.";
    }

    private static readonly Column InProject = new("IN PROJECT", r => r.Entry!.Label(s => s.DisplayName) ?? "loaded");

    private static readonly Column WhereMissing = new("NOTE", r => r.Missing!.Reason(s => s.DisplayName) ?? string.Empty);

    private static readonly Column[] UsageColumns =
    [
        new("USES", r => (r.Estimated ? "≈" : string.Empty) + (r.Usage?.Count ?? 0).ToString(CultureInfo.InvariantCulture)),
        new("LAST USED", r => r.Usage?.LastUsed.ToLocalTime().ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "never"),
    ];

    public static string Render(IReadOnlyList<MissingEntry> entries) =>
        Build(entries.Select(e => new Row(e.Skill, null, null, e)).ToList(), [.. Base, WhereMissing]);

    public static string Render(IReadOnlyList<MissingEntry> entries, CopyUsage usage) =>
        Build(entries.Select(e => new Row(e.Skill, null, usage.For(e.Skill), e, usage.IsEstimate(e.Skill))).ToList(), [.. Base, WhereMissing, .. UsageColumns]);

    public static string Render(IReadOnlyList<Skill> skills, CopyUsage usage) =>
        Build(skills.Select(s => new Row(s, null, usage.For(s), Estimated: usage.IsEstimate(s))).ToList(), [.. Base, .. UsageColumns]);

    public static string Render(IReadOnlyList<EffectiveEntry> entries, CopyUsage usage) =>
        Build(entries.Select(e => new Row(e.Skill, e, usage.For(e.Skill), Estimated: usage.IsEstimate(e.Skill))).ToList(), [.. Base, InProject, .. UsageColumns]);

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

    private sealed record Row(Skill Skill, EffectiveEntry? Entry, SkillUsage? Usage, MissingEntry? Missing = null, bool Estimated = false);

    private sealed record Column(string Header, Func<Row, string> Value);
}

using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lustrum.Core.Budget;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Usage;

namespace Lustrum.Cli;

internal static class ListCommand
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Command Create()
    {
        var json = new Option<bool>("--json") { Description = "Emit machine-readable JSON" };
        var source = new Option<string?>("--source")
        {
            Description = "Limit to one source kind: managed, global, project, plugin or synced",
        };
        var broken = new Option<bool>("--broken") { Description = "Only skills with a health problem" };
        var usage = new Option<bool>("--usage") { Description = "Add how often and when each skill was used, read from Claude Code transcripts" };
        var unused = new Option<bool>("--unused") { Description = "Only skills with no recorded use in Claude Code transcripts" };
        var missing = new Option<bool>("--missing") { Description = "With --project: skills on this machine that the project does not load" };
        var budget = new Option<bool>("--budget") { Description = "With --project: estimated tokens each skill takes in Claude's skill list, and the total" };
        var project = new Option<string?>("--project")
        {
            Description = "Show what Claude Code loads in this project folder: its skills and those of its parent folders, managed and global skills, enabled plugins, and on demand the skills of folders below it",
        };

        var command = new Command("list", "List skills and commands found on this machine");
        command.Options.Add(json);
        command.Options.Add(source);
        command.Options.Add(broken);
        command.Options.Add(project);
        command.Options.Add(usage);
        command.Options.Add(unused);
        command.Options.Add(missing);
        command.Options.Add(budget);
        command.SetAction(async (parseResult, cancellationToken) =>
            await RunAsync(
                new ListRequest(
                    parseResult.GetValue(json),
                    parseResult.GetValue(source),
                    parseResult.GetValue(broken),
                    parseResult.GetValue(project),
                    parseResult.GetValue(usage),
                    parseResult.GetValue(unused),
                    parseResult.GetValue(missing),
                    parseResult.GetValue(budget)),
                cancellationToken));
        return command;
    }

    private static async Task<int> RunAsync(ListRequest request, CancellationToken cancellationToken)
    {
        SourceKind? kind = null;
        if (request.Source is not null)
        {
            if (!SkillFilter.SourceNames.TryGetValue(request.Source, out var parsed))
            {
                await Console.Error.WriteLineAsync($"Unknown source '{request.Source}'. Use managed, global, project, plugin or synced.");
                return ExitCodes.Usage;
            }

            kind = parsed;
        }

        if (request.Missing && request.Budget)
        {
            await Console.Error.WriteLineAsync("Use --missing or --budget, not both.");
            return ExitCodes.Usage;
        }

        if ((request.Missing || request.Budget) && request.Project is null)
        {
            await Console.Error.WriteLineAsync($"{(request.Missing ? "--missing" : "--budget")} needs --project <folder>.");
            return ExitCodes.Usage;
        }

        var scan = await CliScan.RunAsync(request.Project, cancellationToken);
        CopyUsage? usage = null;
        if (request.WithUsage || request.UnusedOnly)
        {
            var report = await CollectUsageAsync(cancellationToken);
            if (report is null && request.UnusedOnly)
            {
                return ExitCodes.Usage;
            }

            usage = report is null ? null : CopyUsage.Attribute(scan, report);
        }
        var selected = SkillFilter.Apply(scan.Skills, kind, request.BrokenOnly)
            .Where(s => !request.UnusedOnly || usage?.IsUnused(s) != false)
            .ToList();

        if (request.Project is null)
        {
            Console.WriteLine(Plain(selected, request.AsJson, request.WithUsage ? usage : null));
            return 0;
        }

        var target = EffectiveSetBuilder.FindProject(scan, request.Project);
        if (target is null)
        {
            await Console.Error.WriteLineAsync($"No Claude Code project at '{request.Project}': expected a folder with a .claude directory.");
            return ExitCodes.Usage;
        }

        var included = selected.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        if (request.Missing)
        {
            var absent = MissingSetBuilder.Build(scan, target).Entries.Where(e => included.Contains(e.Skill.Id)).ToList();
            var missingTable = request.WithUsage && usage is not null ? SkillTable.Render(absent, usage) : SkillTable.Render(absent);
            Console.WriteLine(request.AsJson
                ? JsonSerializer.Serialize(absent.Select(e => ToJson(e, usage)), JsonOptions)
                : missingTable);
            return 0;
        }

        var set = EffectiveSetBuilder.Build(scan, target);
        var entries = set.Entries.Where(e => included.Contains(e.Skill.Id)).ToList();
        if (request.AsJson)
        {
            Console.WriteLine(JsonSerializer.Serialize(entries.Select(e => ToJson(e, usage)), JsonOptions));
            return 0;
        }

        if (request.Budget)
        {
            Console.WriteLine(SkillTable.RenderBudget(entries));
            Console.WriteLine();
            Console.WriteLine(SkillTable.BudgetLine(ContextBudget.Summarize(set)));
            return 0;
        }

        Console.WriteLine(request.WithUsage && usage is not null ? SkillTable.Render(entries, usage) : SkillTable.Render(entries));
        return 0;
    }

    private static string Plain(IReadOnlyList<Skill> skills, bool asJson, CopyUsage? usage)
    {
        if (!asJson)
        {
            return usage is null ? SkillTable.Render(skills) : SkillTable.Render(skills, usage);
        }

        return usage is null
            ? JsonSerializer.Serialize(skills, JsonOptions)
            : JsonSerializer.Serialize(skills.Select(s => new { Skill = s, Usage = usage.For(s), UsageIsEstimate = usage.IsEstimate(s) }), JsonOptions);
    }

    private static async Task<UsageReport?> CollectUsageAsync(CancellationToken cancellationToken)
    {
        var projects = ClaudePaths.ForCurrentUser().ProjectsDir;
        var report = await new UsageCollector().CollectAsync(projects, cancellationToken);
        foreach (var issue in report.Issues)
        {
            await Console.Error.WriteLineAsync($"warning: {issue.Path}: {issue.Message}");
        }

        if (report.TranscriptCount > 0)
        {
            return report;
        }

        await Console.Error.WriteLineAsync($"No Claude Code transcripts found in '{projects}', so usage is unknown.");
        return null;
    }

    private static object ToJson(MissingEntry entry, CopyUsage? usage) => new
    {
        entry.Skill,
        Usage = usage?.For(entry.Skill),
        UsageIsEstimate = usage?.IsEstimate(entry.Skill),
        Reason = entry.Reason(s => s.DisplayName),
        LoadedHere = entry.LoadedHere?.Id,
    };

    private static object ToJson(EffectiveEntry entry, CopyUsage? usage) => new
    {
        entry.Skill,
        Usage = usage?.For(entry.Skill),
        UsageIsEstimate = usage?.IsEstimate(entry.Skill),
        entry.Availability,
        ShadowedBy = entry.ShadowedBy?.Id,
        Overrides = entry.Overrides.Select(o => o.Id).ToList(),
        Visibility = entry.VisibilityValue.ToSetting(),
        entry.LoadsUnder,
        ListedChars = ContextBudget.ListedChars(entry),
    };

    private sealed record ListRequest(bool AsJson, string? Source, bool BrokenOnly, string? Project, bool WithUsage, bool UnusedOnly, bool Missing, bool Budget);
}

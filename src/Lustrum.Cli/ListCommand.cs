using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.Cli;

internal static class ListCommand
{
    private const int UsageError = 2;

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
            Description = "Limit to one source kind: global, project, plugin or synced",
        };
        var broken = new Option<bool>("--broken") { Description = "Only skills with a health problem" };
        var usage = new Option<bool>("--usage") { Description = "Add how often and when each skill was used, read from Claude Code transcripts" };
        var unused = new Option<bool>("--unused") { Description = "Only skills with no recorded use in Claude Code transcripts" };
        var missing = new Option<bool>("--missing") { Description = "With --project: skills on this machine that the project does not load" };
        var project = new Option<string?>("--project")
        {
            Description = "Show what Claude Code loads in this project folder: its skills, global skills and enabled plugins",
        };

        var command = new Command("list", "List skills and commands found on this machine");
        command.Options.Add(json);
        command.Options.Add(source);
        command.Options.Add(broken);
        command.Options.Add(project);
        command.Options.Add(usage);
        command.Options.Add(unused);
        command.Options.Add(missing);
        command.SetAction(async (parseResult, cancellationToken) =>
            await RunAsync(
                new ListRequest(
                    parseResult.GetValue(json),
                    parseResult.GetValue(source),
                    parseResult.GetValue(broken),
                    parseResult.GetValue(project),
                    parseResult.GetValue(usage),
                    parseResult.GetValue(unused),
                    parseResult.GetValue(missing)),
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
                await Console.Error.WriteLineAsync($"Unknown source '{request.Source}'. Use global, project, plugin or synced.");
                return UsageError;
            }

            kind = parsed;
        }

        if (request.Missing && request.Project is null)
        {
            await Console.Error.WriteLineAsync("--missing needs --project <folder>.");
            return UsageError;
        }

        var scan = await ScanAsync(request.Project, cancellationToken);
        UsageReport? report = null;
        if (request.WithUsage || request.UnusedOnly)
        {
            report = await CollectUsageAsync(cancellationToken);
            if (report is null && request.UnusedOnly)
            {
                return UsageError;
            }
        }
        var selected = SkillFilter.Apply(scan.Skills, kind, request.BrokenOnly)
            .Where(s => !request.UnusedOnly || report?.For(s.FullName) is null)
            .ToList();

        if (request.Project is null)
        {
            Console.WriteLine(Plain(selected, request.AsJson, request.WithUsage ? report : null));
            return 0;
        }

        var target = EffectiveSetBuilder.FindProject(scan, request.Project);
        if (target is null)
        {
            await Console.Error.WriteLineAsync($"No Claude Code project at '{request.Project}': expected a folder with a .claude directory.");
            return UsageError;
        }

        var included = selected.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        if (request.Missing)
        {
            var absent = MissingSetBuilder.Build(scan, target).Entries.Where(e => included.Contains(e.Skill.Id)).ToList();
            Console.WriteLine(request.AsJson
                ? JsonSerializer.Serialize(absent.Select(e => new { e.Skill, LoadedHere = e.LoadedHere?.Id }), JsonOptions)
                : SkillTable.Render(absent));
            return 0;
        }

        var entries = EffectiveSetBuilder.Build(scan, target).Entries.Where(e => included.Contains(e.Skill.Id)).ToList();
        var table = request.WithUsage && report is not null ? SkillTable.Render(entries, report) : SkillTable.Render(entries);
        Console.WriteLine(request.AsJson ? JsonSerializer.Serialize(entries.Select(e => ToJson(e, report)), JsonOptions) : table);
        return 0;
    }

    private static async Task<ScanResult> ScanAsync(string? project, CancellationToken cancellationToken)
    {
        var settings = SettingsStore.Load(SettingsStore.DefaultPath);
        if (settings.Warning is { } warning)
        {
            await Console.Error.WriteLineAsync($"warning: {warning}");
        }

        var options = settings.Settings.ToScanOptions();
        if (project is not null)
        {
            options = options with { ExtraRoots = [.. options.ExtraRoots, Path.GetFullPath(project)] };
        }

        var scan = await SkillScanner.ScanAsync(ClaudePaths.ForCurrentUser(), options, cancellationToken);
        foreach (var issue in scan.Issues)
        {
            await Console.Error.WriteLineAsync($"warning: {issue.Path}: {issue.Message}");
        }

        return scan;
    }

    private static string Plain(IReadOnlyList<Skill> skills, bool asJson, UsageReport? usage)
    {
        if (!asJson)
        {
            return usage is null ? SkillTable.Render(skills) : SkillTable.Render(skills, usage);
        }

        return usage is null
            ? JsonSerializer.Serialize(skills, JsonOptions)
            : JsonSerializer.Serialize(skills.Select(s => new { Skill = s, Usage = usage.For(s.FullName) }), JsonOptions);
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

    private static object ToJson(EffectiveEntry entry, UsageReport? usage) => new
    {
        entry.Skill,
        Usage = usage?.For(entry.Skill.FullName),
        entry.Availability,
        ShadowedBy = entry.ShadowedBy?.Id,
        Overrides = entry.Overrides.Select(o => o.Id).ToList(),
    };

    private sealed record ListRequest(bool AsJson, string? Source, bool BrokenOnly, string? Project, bool WithUsage, bool UnusedOnly, bool Missing);
}

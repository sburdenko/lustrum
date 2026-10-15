using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Settings;

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
            Description = "Limit to one source kind: global, project or plugin",
        };
        var broken = new Option<bool>("--broken") { Description = "Only skills with a health problem" };
        var project = new Option<string?>("--project")
        {
            Description = "Show what Claude Code loads in this project folder: its skills, global skills and enabled plugins",
        };

        var command = new Command("list", "List skills and commands found on this machine");
        command.Options.Add(json);
        command.Options.Add(source);
        command.Options.Add(broken);
        command.Options.Add(project);
        command.SetAction(async (parseResult, cancellationToken) =>
            await RunAsync(
                new ListRequest(parseResult.GetValue(json), parseResult.GetValue(source), parseResult.GetValue(broken), parseResult.GetValue(project)),
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
                await Console.Error.WriteLineAsync($"Unknown source '{request.Source}'. Use global, project or plugin.");
                return UsageError;
            }

            kind = parsed;
        }

        var scan = await ScanAsync(request.Project, cancellationToken);
        if (request.Project is null)
        {
            var skills = SkillFilter.Apply(scan.Skills, kind, request.BrokenOnly).ToList();
            Console.WriteLine(request.AsJson ? JsonSerializer.Serialize(skills, JsonOptions) : SkillTable.Render(skills));
            return 0;
        }

        var target = EffectiveSetBuilder.FindProject(scan, request.Project);
        if (target is null)
        {
            await Console.Error.WriteLineAsync($"No Claude Code project at '{request.Project}': expected a folder with a .claude directory.");
            return UsageError;
        }

        var included = SkillFilter.Apply(scan.Skills, kind, request.BrokenOnly).Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var entries = EffectiveSetBuilder.Build(scan, target).Entries.Where(e => included.Contains(e.Skill.Id)).ToList();
        Console.WriteLine(request.AsJson ? JsonSerializer.Serialize(entries.Select(ToJson), JsonOptions) : SkillTable.Render(entries));
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

    private static object ToJson(EffectiveEntry entry) => new
    {
        entry.Skill,
        entry.Availability,
        ShadowedBy = entry.ShadowedBy?.Id,
        Overrides = entry.Overrides.Select(o => o.Id).ToList(),
    };

    private sealed record ListRequest(bool AsJson, string? Source, bool BrokenOnly, string? Project);
}

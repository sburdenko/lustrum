using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Settings;

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
            Description = "Limit to one source kind: global, project or plugin",
        };
        var broken = new Option<bool>("--broken") { Description = "Only skills with a health problem" };

        var command = new Command("list", "List skills and commands found on this machine");
        command.Options.Add(json);
        command.Options.Add(source);
        command.Options.Add(broken);
        command.SetAction(async (parseResult, cancellationToken) =>
            await RunAsync(parseResult.GetValue(json), parseResult.GetValue(source), parseResult.GetValue(broken), cancellationToken));
        return command;
    }

    private static async Task<int> RunAsync(bool asJson, string? sourceName, bool brokenOnly, CancellationToken cancellationToken)
    {
        SourceKind? kind = null;
        if (sourceName is not null)
        {
            if (!SkillFilter.SourceNames.TryGetValue(sourceName, out var parsed))
            {
                await Console.Error.WriteLineAsync($"Unknown source '{sourceName}'. Use global, project or plugin.");
                return 2;
            }

            kind = parsed;
        }

        var settings = SettingsStore.Load(SettingsStore.DefaultPath);
        if (settings.Warning is { } warning)
        {
            await Console.Error.WriteLineAsync($"warning: {warning}");
        }

        var scan = await SkillScanner.ScanAsync(ClaudePaths.ForCurrentUser(), settings.Settings.ToScanOptions(), cancellationToken);
        foreach (var issue in scan.Issues)
        {
            await Console.Error.WriteLineAsync($"warning: {issue.Path}: {issue.Message}");
        }

        var skills = SkillFilter.Apply(scan.Skills, kind, brokenOnly).ToList();
        Console.WriteLine(asJson ? JsonSerializer.Serialize(skills, JsonOptions) : SkillTable.Render(skills));
        return 0;
    }
}

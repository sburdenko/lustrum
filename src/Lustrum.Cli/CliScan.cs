using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Settings;

namespace Lustrum.Cli;

/// <summary>Scans with the user's Lustrum settings, writing settings and read warnings to stderr.</summary>
internal static class CliScan
{
    public static async Task<ScanResult> RunAsync(string? project, CancellationToken cancellationToken)
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

        var scan = await SkillScanner.ScanAsync(ClaudePaths.ForCurrentUser(), options, cancellationToken, SystemPolicyStore.Instance);
        foreach (var issue in scan.Issues)
        {
            await Console.Error.WriteLineAsync($"warning: {issue.Path}: {issue.Message}");
        }

        return scan;
    }
}

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
        var options = await OptionsAsync();
        if (project is not null)
        {
            options = options with { ExtraRoots = [.. options.ExtraRoots, Path.GetFullPath(project)] };
        }

        var scan = await SkillScanner.ScanAsync(ClaudePaths.ForCurrentUser(), options, cancellationToken, SystemPolicyStore.Instance);
        await WarnAsync(scan);
        return scan;
    }

    /// <summary>Scans only <paramref name="folder"/>, leaving ~/.claude, Claude Code's settings and Lustrum's own out.</summary>
    public static async Task<ScanResult> RunFolderAsync(string folder, CancellationToken cancellationToken)
    {
        var scan = await Task.Run(() => FolderScanner.Scan(folder), cancellationToken);
        await WarnAsync(scan);
        return scan;
    }

    private static async Task<ScanOptions> OptionsAsync()
    {
        var settings = SettingsStore.Load(SettingsStore.DefaultPath);
        if (settings.Warning is { } warning)
        {
            await Console.Error.WriteLineAsync($"warning: {warning}");
        }

        return settings.Settings.ToScanOptions();
    }

    private static async Task WarnAsync(ScanResult scan)
    {
        foreach (var issue in scan.Issues)
        {
            await Console.Error.WriteLineAsync($"warning: {issue.Path}: {issue.Message}");
        }
    }
}

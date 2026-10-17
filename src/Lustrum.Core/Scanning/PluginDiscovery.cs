using System.Text.Json;
using Lustrum.Core.Models;

namespace Lustrum.Core.Scanning;

internal static class PluginDiscovery
{
    private const string OrphanMarker = ".orphaned_at";

    public static IEnumerable<SkillSource> Discover(ClaudePaths paths, Action<ScanIssue> report)
    {
        var disabled = DisabledPlugins(paths.SettingsJson, report);
        var installed = File.Exists(paths.InstalledPluginsJson)
            ? Installed(paths.InstalledPluginsJson, report)
            : Cached(paths.PluginCacheDir, report);

        return installed
            .Select(p => Source(p.Marketplace, p.Plugin, p.InstallPath, !disabled.Contains($"{p.Plugin}@{p.Marketplace}")))
            .ToList();
    }

    private static IReadOnlyList<(string Marketplace, string Plugin, string InstallPath)> Installed(string registryPath, Action<ScanIssue> report)
    {
        var found = new List<(string, string, string)>();
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(registryPath));
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("plugins", out var plugins)
                || plugins.ValueKind != JsonValueKind.Object)
            {
                report(new ScanIssue(registryPath, "Installed plugins file has an unexpected shape"));
                return found;
            }

            foreach (var plugin in plugins.EnumerateObject())
            {
                var (name, marketplace) = SplitKey(plugin.Name);
                var installPath = InstallPathOf(plugin.Value);
                if (installPath is not null)
                {
                    found.Add((marketplace, name, installPath));
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException or InvalidOperationException)
        {
            report(new ScanIssue(registryPath, $"Cannot read installed plugins: {ex.Message}"));
        }

        return found;
    }

    private static IReadOnlyList<(string Marketplace, string Plugin, string InstallPath)> Cached(string cacheDir, Action<ScanIssue> report)
    {
        var found = new List<(string, string, string)>();
        foreach (var marketplaceDir in SafeFileSystem.Directories(cacheDir, report))
        {
            foreach (var pluginDir in SafeFileSystem.Directories(marketplaceDir, report))
            {
                var live = SafeFileSystem.Directories(pluginDir, report)
                    .Where(version => !File.Exists(Path.Combine(version, OrphanMarker)))
                    .OrderByDescending(Directory.GetLastWriteTimeUtc)
                    .FirstOrDefault();

                if (live is not null)
                {
                    found.Add((Path.GetFileName(marketplaceDir), Path.GetFileName(pluginDir), live));
                }
            }
        }

        return found;
    }

    private static HashSet<string> DisabledPlugins(string settingsPath, Action<ScanIssue> report)
    {
        var disabled = new HashSet<string>(StringComparer.Ordinal);
        if (!File.Exists(settingsPath))
        {
            return disabled;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(settingsPath));
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("enabledPlugins", out var enabled)
                && enabled.ValueKind == JsonValueKind.Object)
            {
                foreach (var entry in enabled.EnumerateObject().Where(e => e.Value.ValueKind == JsonValueKind.False))
                {
                    disabled.Add(entry.Name);
                }
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            report(new ScanIssue(settingsPath, $"Cannot read Claude Code settings: {ex.Message}"));
        }

        return disabled;
    }

    private static (string Plugin, string Marketplace) SplitKey(string key)
    {
        var at = key.LastIndexOf('@');
        return at > 0 ? (key[..at], key[(at + 1)..]) : (key, "local");
    }

    private static string? InstallPathOf(JsonElement installs) =>
        installs.ValueKind == JsonValueKind.Array
            ? installs.EnumerateArray()
                .Select(InstallPathOfEntry)
                .OfType<string>()
                .Where(Path.IsPathRooted)
                .OrderByDescending(Directory.Exists)
                .FirstOrDefault()
            : null;

    private static string? InstallPathOfEntry(JsonElement install) =>
        install.ValueKind == JsonValueKind.Object
        && install.TryGetProperty("installPath", out var path)
        && path.ValueKind == JsonValueKind.String
            ? path.GetString()
            : null;

    private static SkillSource Source(string marketplace, string plugin, string installPath, bool enabled) =>
        new(
            $"plugin:{marketplace}/{plugin}",
            SourceKind.Plugin,
            $"{plugin} ({marketplace})",
            installPath,
            Directory.Exists(installPath) ? Directory.GetLastWriteTimeUtc(installPath) : null,
            Directory.Exists(installPath),
            enabled,
            plugin);
}

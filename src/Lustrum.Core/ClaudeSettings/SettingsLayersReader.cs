using System.Collections.Immutable;
using System.Text.Json;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.ClaudeSettings;

public static class SettingsLayersReader
{
    private const string SkillsKey = "skillOverrides";
    private const string PluginsKey = "enabledPlugins";

    public static SettingsLayers Read(ClaudePaths paths, IEnumerable<SkillSource> sources, Action<ScanIssue> report)
    {
        var projects = sources
            .Where(s => s.Kind == SourceKind.Project && s.Exists)
            .ToImmutableDictionary(
                s => s.Id,
                s => new ProjectSettings(
                    ReadLayer(SettingsScope.Project, ProjectSettings.SharedPath(s), report),
                    ReadLayer(SettingsScope.Local, ProjectSettings.LocalPath(s), report)),
                StringComparer.Ordinal);
        return ReadMachine(paths, report) with { Projects = projects };
    }

    /// <summary>Managed and user settings only, which is all that decides a plugin's state outside a project.</summary>
    public static SettingsLayers ReadMachine(ClaudePaths paths, Action<ScanIssue> report) =>
        SettingsLayers.Empty with
        {
            User = ReadLayer(SettingsScope.User, paths.SettingsJson, report),
            Managed = ReadManaged(paths, report),
        };

    // managed-settings.json comes first, then every *.json in managed-settings.d in name order, each overriding the last.
    private static SettingsLayer? ReadManaged(ClaudePaths paths, Action<ScanIssue> report)
    {
        var files = new List<string>();
        if (File.Exists(paths.ManagedSettingsJson))
        {
            files.Add(paths.ManagedSettingsJson);
        }

        if (Directory.Exists(paths.ManagedSettingsDropIns))
        {
            files.AddRange(SafeFileSystem.Files(paths.ManagedSettingsDropIns, "*.json", new EnumerationOptions { IgnoreInaccessible = true }, report)
                .Where(f => !Path.GetFileName(f).StartsWith('.'))
                .Order(StringComparer.Ordinal));
        }

        return files.Count == 0
            ? null
            : files.Select(f => ReadLayer(SettingsScope.Managed, f, report))
                .Aggregate((merged, next) => merged.MergedWith(next)) with { Path = paths.ManagedSettingsJson };
    }

    public static SettingsLayer ReadLayer(SettingsScope scope, string path, Action<ScanIssue> report)
    {
        var empty = SettingsLayer.Empty(scope, path);
        if (!File.Exists(path))
        {
            return empty;
        }

        try
        {
            using var doc = JsonDocument.Parse(SharedFile.ReadAllText(path));
            return doc.RootElement.ValueKind == JsonValueKind.Object
                ? empty with { Skills = Skills(doc.RootElement, empty.Skills), Plugins = Plugins(doc.RootElement) }
                : empty;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            report(new ScanIssue(path, $"Cannot read Claude Code settings: {ex.Message}"));
            return empty;
        }
    }

    private static ImmutableDictionary<string, SkillVisibility> Skills(JsonElement root, ImmutableDictionary<string, SkillVisibility> skills)
    {
        if (!root.TryGetProperty(SkillsKey, out var overrides) || overrides.ValueKind != JsonValueKind.Object)
        {
            return skills;
        }

        foreach (var property in overrides.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.String && SkillVisibilityValues.FromSetting(property.Value.GetString()) is { } value)
            {
                skills = skills.SetItem(property.Name, value);
            }
        }

        return skills;
    }

    private static ImmutableDictionary<string, bool> Plugins(JsonElement root)
    {
        var plugins = ImmutableDictionary<string, bool>.Empty.WithComparers(StringComparer.Ordinal);
        if (!root.TryGetProperty(PluginsKey, out var enabled) || enabled.ValueKind != JsonValueKind.Object)
        {
            return plugins;
        }

        foreach (var property in enabled.EnumerateObject())
        {
            if (property.Value.ValueKind is JsonValueKind.True or JsonValueKind.False)
            {
                plugins = plugins.SetItem(property.Name, property.Value.GetBoolean());
            }
        }

        return plugins;
    }
}

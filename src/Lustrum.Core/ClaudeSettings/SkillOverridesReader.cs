using System.Collections.Immutable;
using System.Text.Json;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.ClaudeSettings;

public static class SkillOverridesReader
{
    private const string Key = "skillOverrides";

    public static SkillOverrides Read(ClaudePaths paths, IEnumerable<SkillSource> sources, Action<ScanIssue> report)
    {
        // A broken user settings file is already reported when plugins are discovered.
        var user = ReadLayer(SettingsScope.User, paths.SettingsJson, _ => { });
        var projects = sources
            .Where(s => s.Kind == SourceKind.Project && s.Exists)
            .ToImmutableDictionary(
                s => s.Id,
                s => new ProjectSettings(
                    ReadLayer(SettingsScope.Project, ProjectSettings.SharedPath(s), report),
                    ReadLayer(SettingsScope.Local, ProjectSettings.LocalPath(s), report)),
                StringComparer.Ordinal);
        return new SkillOverrides(user, projects);
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
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty(Key, out var overrides)
                || overrides.ValueKind != JsonValueKind.Object)
            {
                return empty;
            }

            var skills = empty.Skills;
            foreach (var property in overrides.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String && SkillVisibilityValues.FromSetting(property.Value.GetString()) is { } value)
                {
                    skills = skills.SetItem(property.Name, value);
                }
            }

            return empty with { Skills = skills };
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            report(new ScanIssue(path, $"Cannot read Claude Code settings: {ex.Message}"));
            return empty;
        }
    }
}

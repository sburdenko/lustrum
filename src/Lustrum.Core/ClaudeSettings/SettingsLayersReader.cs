using System.Collections.Immutable;
using System.Text.Json;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.ClaudeSettings;

public static class SettingsLayersReader
{
    private const string SkillsKey = "skillOverrides";
    private const string PluginsKey = "enabledPlugins";

    private const string MergeKey = "managedSourcesBehavior";

    public static SettingsLayers Read(ClaudePaths paths, IEnumerable<SkillSource> sources, Action<ScanIssue> report, IPolicyStore? policies = null)
    {
        var projects = sources
            .Where(s => s.Kind == SourceKind.Project && s.Exists)
            .ToImmutableDictionary(
                s => s.Id,
                s => new ProjectSettings(
                    ReadLayer(SettingsScope.Project, ProjectSettings.SharedPath(s), report),
                    ReadLayer(SettingsScope.Local, ProjectSettings.LocalPath(s), report)),
                StringComparer.Ordinal);
        return ReadMachine(paths, report, policies) with { Projects = projects };
    }

    /// <summary>Managed and user settings only, which is all that decides a plugin's state outside a project.</summary>
    public static SettingsLayers ReadMachine(ClaudePaths paths, Action<ScanIssue> report, IPolicyStore? policies = null) =>
        SettingsLayers.Empty with
        {
            User = ReadLayer(SettingsScope.User, paths.SettingsJson, report),
            Managed = ReadManaged(paths, policies ?? NoPolicyStore.Instance, report),
        };

    /// <summary>
    /// Picks the managed settings the way Claude Code ranks its admin sources: the server-managed cache, then device
    /// policy, then managed files, the first one with a policy key winning. With managedSourcesBehavior set to
    /// "merge" in the highest present source, every admin source contributes, higher ones winning key by key. The
    /// user-writable HKCU value applies only when no admin source is present at all.
    /// </summary>
    private static SettingsLayer? ReadManaged(ClaudePaths paths, IPolicyStore policies, Action<ScanIssue> report)
    {
        var admin = new[] { Remote(paths, report), Device(policies, report), Files(paths, report) }
            .OfType<PolicyDocument>()
            .ToList();
        if (admin.Count == 0)
        {
            return policies.UserPolicy() is { } user ? Parse(user, report).Layer : null;
        }

        if (admin[0].AsksToMerge)
        {
            var contributing = admin.Where(d => d.HasPolicy).Reverse().Select(d => d.Layer).ToList();
            return contributing.Count == 0
                ? null
                : contributing.Aggregate((merged, higher) => merged.MergedWith(higher)) with { Path = admin[0].Layer.Path };
        }

        return admin.FirstOrDefault(d => d.HasPolicy)?.Layer;
    }

    private static PolicyDocument? Remote(ClaudePaths paths, Action<ScanIssue> report) =>
        File.Exists(paths.RemoteSettingsJson) ? Parse(FromFile(paths.RemoteSettingsJson), report) : null;

    private static PolicyDocument? Device(IPolicyStore policies, Action<ScanIssue> report)
    {
        var documents = policies.DevicePolicies().Select(p => Parse(p, report)).ToList();
        return documents.Count == 0 ? null : Combine(documents);
    }

    // managed-settings.json comes first, then every *.json in managed-settings.d in name order, each overriding the last.
    private static PolicyDocument? Files(ClaudePaths paths, Action<ScanIssue> report)
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

        if (files.Count == 0)
        {
            return null;
        }

        var merged = Combine(files.Select(f => Parse(FromFile(f), report)).ToList());
        return merged with { Layer = merged.Layer with { Path = paths.ManagedSettingsJson } };
    }

    private static PolicyDocument Combine(IReadOnlyList<PolicyDocument> documents) => documents.Aggregate((merged, next) => new PolicyDocument(
        merged.Layer.MergedWith(next.Layer) with { Path = next.Layer.Path },
        merged.HasPolicy || next.HasPolicy,
        merged.AsksToMerge || next.AsksToMerge));

    private static PolicyText FromFile(string path)
    {
        try
        {
            return new PolicyText(path, SharedFile.ReadAllText(path));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new PolicyText(path, null, ex.Message);
        }
    }

    private static PolicyDocument Parse(PolicyText text, Action<ScanIssue> report)
    {
        var empty = SettingsLayer.Empty(SettingsScope.Managed, text.Label);
        if (text.Json is null)
        {
            report(new ScanIssue(text.Label, $"Cannot read managed settings: {text.Error}"));
            return new PolicyDocument(empty, false, false);
        }

        try
        {
            using var doc = JsonDocument.Parse(text.Json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                report(new ScanIssue(text.Label, "Managed settings are not a JSON object"));
                return new PolicyDocument(empty, false, false);
            }

            var root = doc.RootElement;
            var layer = empty with { Skills = Skills(root, empty.Skills), Plugins = Plugins(root) };
            var merge = root.TryGetProperty(MergeKey, out var behavior) && behavior.ValueKind == JsonValueKind.String && behavior.GetString() == "merge";
            return new PolicyDocument(layer, root.EnumerateObject().Any(p => p.Name != MergeKey), merge);
        }
        catch (JsonException ex)
        {
            report(new ScanIssue(text.Label, $"Cannot read managed settings: {ex.Message}"));
            return new PolicyDocument(empty, false, false);
        }
    }

    private sealed record PolicyDocument(SettingsLayer Layer, bool HasPolicy, bool AsksToMerge);

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

using System.Collections.Immutable;

namespace Lustrum.Core.ClaudeSettings;

/// <summary>The keys Lustrum reads from one Claude Code settings file: <c>skillOverrides</c> and <c>enabledPlugins</c>.</summary>
public sealed record SettingsLayer(
    SettingsScope Scope,
    string Path,
    ImmutableDictionary<string, SkillVisibility> Skills,
    ImmutableDictionary<string, bool>? Plugins = null)
{
    public ImmutableDictionary<string, bool> PluginSwitches => Plugins ?? ImmutableDictionary<string, bool>.Empty;

    public static SettingsLayer Empty(SettingsScope scope, string path) =>
        new(scope, path, ImmutableDictionary<string, SkillVisibility>.Empty.WithComparers(StringComparer.Ordinal));

    public SkillVisibility? Find(string skillName) => Skills.TryGetValue(skillName, out var value) ? value : null;

    public bool? FindPlugin(string pluginKey) => PluginSwitches.TryGetValue(pluginKey, out var value) ? value : null;

    /// <summary>Layers merged key by key, the later one winning; used for managed-settings.json and its drop-ins.</summary>
    public SettingsLayer MergedWith(SettingsLayer later) => this with
    {
        Skills = Skills.SetItems(later.Skills),
        Plugins = PluginSwitches.SetItems(later.PluginSwitches),
    };
}

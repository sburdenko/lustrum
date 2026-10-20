using System.Collections.Immutable;
using Lustrum.Core.Models;

namespace Lustrum.Core.ClaudeSettings;

/// <summary>
/// Claude Code settings that change what loads: managed settings, the user's settings and every project's shared and
/// local settings. A higher file wins key by key: managed, then local, then project, then user.
/// </summary>
public sealed record SettingsLayers(SettingsLayer User, ImmutableDictionary<string, ProjectSettings> Projects, SettingsLayer? Managed = null)
{
    public static SettingsLayers Empty { get; } = new(
        SettingsLayer.Empty(SettingsScope.User, string.Empty),
        ImmutableDictionary<string, ProjectSettings>.Empty.WithComparers(StringComparer.Ordinal));

    /// <summary>Claude Code applies skill overrides to managed, personal and project skills; plugin skills and commands are out of scope.</summary>
    public static bool Applies(Skill skill) =>
        skill.Kind == SkillKind.Skill && skill.Source.Kind is SourceKind.Managed or SourceKind.Global or SourceKind.Project;

    /// <summary>The <c>enabledPlugins</c> key of a plugin source, <c>plugin@marketplace</c>.</summary>
    public static string? PluginKey(SkillSource source)
    {
        const string prefix = "plugin:";
        if (source.Kind != SourceKind.Plugin || !source.Id.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var tail = source.Id[prefix.Length..];
        var slash = tail.IndexOf('/');
        return slash < 0 ? null : $"{tail[(slash + 1)..]}@{tail[..slash]}";
    }

    /// <summary>What a session in <paramref name="project"/> applies to a skill with this name; null project means managed and user settings only.</summary>
    public ResolvedVisibility Resolve(string skillName, SkillSource? project)
    {
        foreach (var layer in LayersFor(project))
        {
            if (layer.Find(skillName) is { } value)
            {
                return new ResolvedVisibility(value, layer);
            }
        }

        return ResolvedVisibility.Default;
    }

    /// <summary>The context a skill is judged in when no project is selected: its own project, or none for any other skill.</summary>
    public ResolvedVisibility ResolveAtHome(Skill skill) =>
        Resolve(skill.Name, skill.Source.Kind == SourceKind.Project ? skill.Source : null);

    /// <summary>Whether a plugin is on in a session started in <paramref name="project"/>; a plugin no file mentions is on.</summary>
    public bool PluginEnabled(SkillSource plugin, SkillSource? project)
    {
        if (PluginKey(plugin) is not { } key)
        {
            return plugin.IsEnabled;
        }

        foreach (var layer in LayersFor(project))
        {
            if (layer.FindPlugin(key) is { } enabled)
            {
                return enabled;
            }
        }

        return true;
    }

    /// <summary>
    /// Where the Visibility control writes: user settings for a personal skill, and settings.local.json for a project
    /// skill, as Claude Code's /skills menu does. That is the skill's own project, or the <paramref name="session"/>
    /// project when the skill comes from one of its parent or nested folders, because only the session's settings
    /// apply there. Managed skills are not editable.
    /// </summary>
    public SettingsLayer? WritableLayerFor(Skill skill, SkillSource? session = null)
    {
        if (!Applies(skill))
        {
            return null;
        }

        return skill.Source.Kind switch
        {
            SourceKind.Global => User,
            SourceKind.Project => ProjectFor(ProjectContextFor(skill, session)!).Local,
            _ => null,
        };
    }

    /// <summary>The project whose settings decide a project skill: the session's when given, else the skill's own.</summary>
    public static SkillSource? ProjectContextFor(Skill skill, SkillSource? session) =>
        session is { Kind: SourceKind.Project } ? session : skill.Source.Kind == SourceKind.Project ? skill.Source : null;

    /// <summary>
    /// The value to store for <paramref name="wanted"/> in <paramref name="layer"/>: null removes the entry, which is
    /// only right for "on" when no lower file would then turn the skill off again.
    /// </summary>
    public SkillVisibility? EntryFor(Skill skill, SettingsLayer layer, SkillVisibility wanted, SkillSource? session = null)
    {
        if (wanted != SkillVisibility.On)
        {
            return wanted;
        }

        var below = LayersFor(ProjectContextFor(skill, session))
            .SkipWhile(l => l.Scope != layer.Scope)
            .Skip(1);
        return below.Any(l => l.Find(skill.Name) is { } value && value != SkillVisibility.On) ? SkillVisibility.On : null;
    }

    public ProjectSettings ProjectFor(SkillSource project) =>
        Projects.GetValueOrDefault(project.Id) ?? ProjectSettings.Empty(project);

    private IEnumerable<SettingsLayer> LayersFor(SkillSource? project)
    {
        if (Managed is { } managed)
        {
            yield return managed;
        }

        if (project is { Kind: SourceKind.Project })
        {
            var settings = ProjectFor(project);
            yield return settings.Local;
            yield return settings.Shared;
        }

        yield return User;
    }
}

using System.Collections.Immutable;
using Lustrum.Core.Models;

namespace Lustrum.Core.ClaudeSettings;

/// <summary>
/// <c>skillOverrides</c> from the user's settings and from every project's shared and local settings. Entries are
/// keyed by skill name, so one entry covers every skill with that name; a higher file wins key by key.
/// </summary>
public sealed record SkillOverrides(SettingsLayer User, ImmutableDictionary<string, ProjectSettings> Projects)
{
    public static SkillOverrides Empty { get; } = new(
        SettingsLayer.Empty(SettingsScope.User, string.Empty),
        ImmutableDictionary<string, ProjectSettings>.Empty.WithComparers(StringComparer.Ordinal));

    /// <summary>Claude Code applies overrides to personal and project skills; plugin skills and commands are out of scope.</summary>
    public static bool Applies(Skill skill) =>
        skill.Kind == SkillKind.Skill && skill.Source.Kind is SourceKind.Global or SourceKind.Project;

    /// <summary>What a session in <paramref name="project"/> applies to a skill with this name; null project means user settings only.</summary>
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

    /// <summary>The context a skill is judged in when no project is selected: its own project, or none for a personal skill.</summary>
    public ResolvedVisibility ResolveAtHome(Skill skill) =>
        Resolve(skill.Name, skill.Source.Kind == SourceKind.Project ? skill.Source : null);

    /// <summary>
    /// Where the Visibility control writes: user settings for a personal skill, and the project's own
    /// settings.local.json for a project skill, as Claude Code's /skills menu does.
    /// </summary>
    public SettingsLayer? WritableLayerFor(Skill skill)
    {
        if (!Applies(skill))
        {
            return null;
        }

        return skill.Source.Kind == SourceKind.Global ? User : ProjectFor(skill.Source).Local;
    }

    /// <summary>
    /// The value to store for <paramref name="wanted"/> in <paramref name="layer"/>: null removes the entry, which is
    /// only right for "on" when no lower file would then turn the skill off again.
    /// </summary>
    public SkillVisibility? EntryFor(Skill skill, SettingsLayer layer, SkillVisibility wanted)
    {
        if (wanted != SkillVisibility.On)
        {
            return wanted;
        }

        var below = LayersFor(skill.Source.Kind == SourceKind.Project ? skill.Source : null)
            .SkipWhile(l => l.Scope != layer.Scope)
            .Skip(1);
        return below.Any(l => l.Find(skill.Name) is { } value && value != SkillVisibility.On) ? SkillVisibility.On : null;
    }

    public ProjectSettings ProjectFor(SkillSource project) =>
        Projects.GetValueOrDefault(project.Id) ?? ProjectSettings.Empty(project);

    private IEnumerable<SettingsLayer> LayersFor(SkillSource? project)
    {
        if (project is { Kind: SourceKind.Project })
        {
            var settings = ProjectFor(project);
            yield return settings.Local;
            yield return settings.Shared;
        }

        yield return User;
    }
}

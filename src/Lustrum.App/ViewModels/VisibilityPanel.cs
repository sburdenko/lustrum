using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Effective;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

/// <summary>
/// What the card's visibility control shows and edits. Current is what applies to the skill at home (its own project,
/// or user settings for a personal skill); Layer is the file a change goes to.
/// </summary>
public sealed record VisibilityPanel(SettingsLayer Layer, SkillVisibility Current, string Hint, string? Note, SkillSource? Session = null)
{
    public static VisibilityPanel? For(Skill skill, SettingsLayers overrides, EffectiveEntry? entry, SkillSource? project)
    {
        var session = skill.Source.Kind == SourceKind.Project && project is { Kind: SourceKind.Project } && project.Id != skill.Source.Id
            ? project
            : null;
        if (overrides.WritableLayerFor(skill, session) is not { } layer)
        {
            return null;
        }

        var home = session is null ? overrides.ResolveAtHome(skill) : overrides.Resolve(skill.Name, session);
        var where = skill.Source.Kind == SourceKind.Global
            ? "Saved in your user settings for every project"
            : $"Saved in {(session ?? skill.Source).DisplayName}/.claude/settings.local.json, for you only";
        var hint = $"{where}. Claude Code matches it by name, so it covers every skill named {skill.Name}.";
        return new VisibilityPanel(layer, home.Value, hint, NoteFor(home, layer, entry, project), session);
    }

    private static string? NoteFor(ResolvedVisibility home, SettingsLayer layer, EffectiveEntry? entry, SkillSource? project)
    {
        if (project is not null && entry?.Visibility is { From: { } from } there && there.Value != home.Value)
        {
            return $"In {project.DisplayName} it is {Label(there.Value)}, set in {FileName(from)}, which takes precedence";
        }

        return home.From is { } source && source.Scope != layer.Scope
            ? $"Currently {Label(home.Value)} because of {FileName(source)}"
            : null;
    }

    private static string Label(SkillVisibility value) => VisibilityOption.For(value).Label;

    private static string FileName(SettingsLayer layer) => layer.Scope switch
    {
        SettingsScope.Local => ".claude/settings.local.json",
        SettingsScope.Project => ".claude/settings.json",
        SettingsScope.Managed => "managed settings",
        _ => "your user settings",
    };
}

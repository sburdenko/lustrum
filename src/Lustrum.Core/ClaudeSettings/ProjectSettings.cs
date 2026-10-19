using Lustrum.Core.Models;

namespace Lustrum.Core.ClaudeSettings;

public sealed record ProjectSettings(SettingsLayer Shared, SettingsLayer Local)
{
    public static string SharedPath(SkillSource project) => Path.Combine(project.RootPath, "settings.json");

    public static string LocalPath(SkillSource project) => Path.Combine(project.RootPath, "settings.local.json");

    public static ProjectSettings Empty(SkillSource project) => new(
        SettingsLayer.Empty(SettingsScope.Project, SharedPath(project)),
        SettingsLayer.Empty(SettingsScope.Local, LocalPath(project)));
}

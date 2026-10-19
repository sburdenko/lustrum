namespace Lustrum.Core.ClaudeSettings;

/// <summary>The four states of Claude Code's <c>skillOverrides</c> setting.</summary>
public enum SkillVisibility { On, NameOnly, UserInvocableOnly, Off }

public static class SkillVisibilityValues
{
    public static string ToSetting(this SkillVisibility visibility) => visibility switch
    {
        SkillVisibility.NameOnly => "name-only",
        SkillVisibility.UserInvocableOnly => "user-invocable-only",
        SkillVisibility.Off => "off",
        _ => "on",
    };

    public static SkillVisibility? FromSetting(string? value) => value switch
    {
        "on" => SkillVisibility.On,
        "name-only" => SkillVisibility.NameOnly,
        "user-invocable-only" => SkillVisibility.UserInvocableOnly,
        "off" => SkillVisibility.Off,
        _ => null,
    };

    /// <summary>A short label for lists; null for the default state.</summary>
    public static string? Label(this SkillVisibility visibility) => visibility switch
    {
        SkillVisibility.NameOnly => "name only",
        SkillVisibility.UserInvocableOnly => "you only",
        SkillVisibility.Off => "off",
        _ => null,
    };
}

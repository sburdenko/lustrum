namespace Lustrum.Core.ClaudeSettings;

/// <summary>The visibility that applies and the settings file it comes from; From is null for the default.</summary>
public sealed record ResolvedVisibility(SkillVisibility Value, SettingsLayer? From)
{
    public static ResolvedVisibility Default { get; } = new(SkillVisibility.On, null);
}

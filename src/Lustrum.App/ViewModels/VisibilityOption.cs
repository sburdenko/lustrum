using Lustrum.Core.ClaudeSettings;

namespace Lustrum.App.ViewModels;

public sealed record VisibilityOption(SkillVisibility Value, string Label, string Description)
{
    public static IReadOnlyList<VisibilityOption> All { get; } =
    [
        new(SkillVisibility.On, "On", "Claude sees it and you can type /name"),
        new(SkillVisibility.NameOnly, "Name only", "Claude sees the name without the description"),
        new(SkillVisibility.UserInvocableOnly, "You only", "Hidden from Claude; you can still type /name"),
        new(SkillVisibility.Off, "Off", "Hidden from Claude and from the / menu"),
    ];

    public static VisibilityOption For(SkillVisibility value) => All.Single(o => o.Value == value);
}

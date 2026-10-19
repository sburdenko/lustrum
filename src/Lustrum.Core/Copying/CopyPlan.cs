using Lustrum.Core.Models;

namespace Lustrum.Core.Copying;

public abstract record CopyPlan(Skill Skill, SkillSource Target);

/// <summary>From is a skill folder or a command file; To is where it will appear in the target source.</summary>
public sealed record ReadyCopy(Skill Skill, SkillSource Target, string From, string To) : CopyPlan(Skill, Target)
{
    public bool IsFolder => Skill.Kind == SkillKind.Skill;
}

public sealed record BlockedCopy(Skill Skill, SkillSource Target, string Reason) : CopyPlan(Skill, Target);

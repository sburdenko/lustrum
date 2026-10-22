using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public sealed record ChipContext(UsageState Usage, UpdateIndex Updates);

public abstract record ChipFilter
{
    public virtual bool IsAvailable(ChipContext context) => true;

    public abstract bool Matches(Skill skill, ChipContext context);
}

public sealed record TagChip(string Tag) : ChipFilter
{
    public override bool Matches(Skill skill, ChipContext context) => skill.Tags.Contains(Tag);
}

public sealed record UnusedChip : ChipFilter
{
    public override bool IsAvailable(ChipContext context) => context.Usage.CanJudgeUnused;

    public override bool Matches(Skill skill, ChipContext context) => context.Usage.IsUnused(skill);
}

public sealed record UpdatesChip : ChipFilter
{
    public override bool IsAvailable(ChipContext context) => context.Updates.HasChecked;

    public override bool Matches(Skill skill, ChipContext context) => context.Updates.Incoming(skill) > 0;
}

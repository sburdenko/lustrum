using Lustrum.Core.Models;
using Lustrum.Core.Usage;

namespace Lustrum.App.ViewModels;

public abstract record ChipFilter
{
    public virtual bool NeedsUsage => false;

    public abstract bool Matches(Skill skill, UsageReport? usage);
}

public sealed record TagChip(string Tag) : ChipFilter
{
    public override bool Matches(Skill skill, UsageReport? usage) => skill.Tags.Contains(Tag);
}

public sealed record UnusedChip : ChipFilter
{
    public override bool NeedsUsage => true;

    public override bool Matches(Skill skill, UsageReport? usage) => usage?.For(skill.FullName) is null;
}

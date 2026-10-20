using Lustrum.Core.Effective;
using Lustrum.Core.Models;

namespace Lustrum.Core.Indexing;

public abstract record SkillScope
{
    public static SkillScope All { get; } = new AllScope();

    public static SkillScope Problems { get; } = new ProblemsScope();

    public abstract bool Includes(Skill skill);
}

public sealed record AllScope : SkillScope
{
    public override bool Includes(Skill skill) => true;
}

public sealed record ProblemsScope : SkillScope
{
    public override bool Includes(Skill skill) => skill.IsBroken || skill.Health == Health.DescriptionTooLong;
}

public sealed record KindScope(SourceKind Kind) : SkillScope
{
    public override bool Includes(Skill skill) => skill.Source.Kind == Kind;
}

public sealed record SourceScope(string SourceId) : SkillScope
{
    public override bool Includes(Skill skill) => skill.Source.Id == SourceId;
}

public sealed record MarketplaceScope(string Marketplace) : SkillScope
{
    public override bool Includes(Skill skill) =>
        skill.Source.Kind == SourceKind.Plugin
        && skill.Source.Id.StartsWith($"plugin:{Marketplace}/", StringComparison.Ordinal);
}

public sealed record EffectiveScope(ProjectContext Context) : SkillScope
{
    public override bool Includes(Skill skill) => Context.Includes(skill);
}

public sealed record MissingScope(ProjectContext Context) : SkillScope
{
    public override bool Includes(Skill skill) => !Context.Includes(skill);
}

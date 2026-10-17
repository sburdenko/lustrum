namespace Lustrum.Core.Comparing;

public sealed record SkillDiff(IReadOnlyList<DiffLine> Lines)
{
    public int Added => Lines.Count(l => l.Kind == DiffKind.Added);

    public int Removed => Lines.Count(l => l.Kind == DiffKind.Removed);

    public bool IsIdentical => Added == 0 && Removed == 0;
}

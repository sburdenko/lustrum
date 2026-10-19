namespace Lustrum.Core.Git;

public sealed record SkillRepository(string Root, RepositoryRole Role)
{
    public string Name => Path.GetFileName(Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
}

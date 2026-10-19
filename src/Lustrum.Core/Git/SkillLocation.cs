namespace Lustrum.Core.Git;

/// <summary>A skill inside a repository; RelativePath is the skill folder (or command file) from the root, "." for the root itself.</summary>
public sealed record SkillLocation(SkillRepository Repository, string RelativePath)
{
    public bool Contains(GitChange change) =>
        RelativePath == "." || change.Path == RelativePath || change.Path.StartsWith(RelativePath + "/", StringComparison.Ordinal);
}

using Lustrum.Core.Models;

namespace Lustrum.Core.Git;

/// <summary>
/// Finds the repository a skill lives in by walking up to a .git entry, without starting git. Plugin and synced
/// skills are left out: Claude Code and the desktop app manage those folders.
/// </summary>
public static class GitRepositoryLocator
{
    public static bool Applies(Skill skill) => skill.Source.Kind is SourceKind.Global or SourceKind.Project;

    public static SkillLocation? Locate(Skill skill)
    {
        if (!Applies(skill))
        {
            return null;
        }

        var content = RealPath.Resolve(ContentPath(skill));
        var root = FindRoot(skill.Kind == SkillKind.Command ? Path.GetDirectoryName(content)! : content);
        if (root is null)
        {
            return null;
        }

        var role = IsSkillsOnly(root, content, skill.Source) ? RepositoryRole.SkillsOnly : RepositoryRole.Containing;
        var relative = Path.GetRelativePath(root, content).Replace(Path.DirectorySeparatorChar, '/');
        return new SkillLocation(new SkillRepository(root, role), relative);
    }

    public static string? FindRoot(string folder)
    {
        for (var current = RealPath.Resolve(folder); current is not null; current = Path.GetDirectoryName(current))
        {
            var marker = Path.Combine(current, ".git");
            if (Directory.Exists(marker) || File.Exists(marker))
            {
                return current;
            }
        }

        return null;
    }

    private static string ContentPath(Skill skill) =>
        skill.Kind == SkillKind.Command ? skill.Path : Path.GetDirectoryName(skill.Path)!;

    private static bool IsSkillsOnly(string root, string content, SkillSource source) =>
        Same(root, content)
        || IsWithin(root, RealPath.Resolve(Path.Combine(source.RootPath, "skills")))
        || IsWithin(root, RealPath.Resolve(Path.Combine(source.RootPath, "commands")));

    private static bool IsWithin(string path, string folder) =>
        Same(path, folder) || path.StartsWith(folder + Path.DirectorySeparatorChar, Comparison);

    private static bool Same(string a, string b) => string.Equals(a, b, Comparison);

    private static StringComparison Comparison =>
        OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}

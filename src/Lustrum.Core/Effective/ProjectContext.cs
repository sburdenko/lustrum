using Lustrum.Core.Git;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.Effective;

/// <summary>
/// What a Claude Code session started in one project can load: that project and every parent up to the repository
/// root at once, folders below it on demand, managed and personal skills, synced skills, and the plugins its
/// settings leave on.
/// </summary>
public sealed record ProjectContext(
    SkillSource Project,
    IReadOnlySet<string> LoadedProjects,
    IReadOnlyDictionary<string, string> OnDemandProjects,
    IReadOnlySet<string> EnabledPlugins)
{
    public static ProjectContext For(ScanResult scan, SkillSource project)
    {
        var folder = project.ProjectFolder ?? project.RootPath;
        var repository = project.Exists ? GitRepositoryLocator.FindRoot(folder) : null;
        var loaded = new HashSet<string>(StringComparer.Ordinal) { project.Id };
        var onDemand = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var other in scan.Sources.Where(s => s.Kind == SourceKind.Project && s.Exists && s.Id != project.Id))
        {
            var otherFolder = other.ProjectFolder ?? other.RootPath;
            if (PathKey.IsWithin(otherFolder, folder))
            {
                var relative = Path.GetRelativePath(folder, otherFolder).Replace(Path.DirectorySeparatorChar, '/');
                if (IsNestedFolder(relative, otherFolder, repository))
                {
                    onDemand[other.Id] = relative;
                }
            }
            else if (repository is not null && PathKey.IsWithin(folder, otherFolder) && PathKey.IsWithin(RealPath.Resolve(otherFolder), repository))
            {
                loaded.Add(other.Id);
            }
        }

        var plugins = scan.Sources
            .Where(s => s.Kind == SourceKind.Plugin && scan.Settings.PluginEnabled(s, project))
            .Select(s => s.Id)
            .ToHashSet(StringComparer.Ordinal);
        return new ProjectContext(project, loaded, onDemand, plugins);
    }

    // Worktrees under .claude/worktrees, nested clones and other repositories are projects of their own, not folders
    // of this one.
    private static bool IsNestedFolder(string relative, string folder, string? repository) =>
        relative.Split('/').All(segment => !segment.StartsWith('.'))
        && (repository is null || string.Equals(GitRepositoryLocator.FindRoot(folder), repository, PathKey.Comparison));

    public bool Includes(Skill skill) => skill.Source.Kind switch
    {
        SourceKind.Managed or SourceKind.Global or SourceKind.BuiltIn => true,
        SourceKind.Plugin => EnabledPlugins.Contains(skill.Source.Id),
        SourceKind.Synced => skill.Source.IsEnabled,
        _ => LoadedProjects.Contains(skill.Source.Id) || OnDemandProjects.ContainsKey(skill.Source.Id),
    };

    /// <summary>The folder, relative to the project, that a skill loads from on demand; null when it loads at start.</summary>
    public string? OnDemandFolder(SkillSource source) => OnDemandProjects.GetValueOrDefault(source.Id);
}

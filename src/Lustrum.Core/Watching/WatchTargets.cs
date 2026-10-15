using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.Watching;

/// <summary>
/// Chooses what to watch. Claude Code writes transcripts, history and worktrees under ~/.claude,
/// ~/.claude/projects and &lt;project&gt;/.claude all the time, so those are watched shallowly (~/.claude only
/// for the names that matter) and only the skill and command folders recursively; otherwise every session
/// would trigger rescans. ~/.claude.json is not watched for the same reason.
/// </summary>
public static class WatchTargets
{
    public static IReadOnlyList<WatchTarget> From(ScanResult scan, ClaudePaths paths)
    {
        var targets = new List<WatchTarget>
        {
            new(paths.ClaudeDir, Recursive: false, Filter: "settings.json"),
            new(paths.ClaudeDir, Recursive: false, Filter: "skills"),
            new(paths.ClaudeDir, Recursive: false, Filter: "commands"),
            new(paths.SkillsDir, Recursive: true),
            new(paths.CommandsDir, Recursive: true),
            new(paths.PluginsDir, Recursive: false),
            new(paths.PluginCacheDir, Recursive: true),
            new(paths.ProjectsDir, Recursive: false),
        };

        foreach (var project in scan.Sources.Where(s => s.Kind == SourceKind.Project && s.Exists))
        {
            targets.Add(new WatchTarget(project.RootPath, Recursive: false));
            targets.Add(new WatchTarget(Path.Combine(project.RootPath, "skills"), Recursive: true));
            targets.Add(new WatchTarget(Path.Combine(project.RootPath, "commands"), Recursive: true));
        }

        return targets.Distinct().ToList();
    }
}

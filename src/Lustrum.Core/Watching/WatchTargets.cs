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
            new(paths.ClaudeDir, Recursive: false, Filter: "remote-settings.json"),
            new(paths.ClaudeDir, Recursive: false, Filter: "skills"),
            new(paths.ClaudeDir, Recursive: false, Filter: "commands"),
            new(paths.SkillsDir, Recursive: true),
            new(paths.CommandsDir, Recursive: true),
            new(paths.PluginsDir, Recursive: false),
            new(paths.PluginCacheDir, Recursive: true),
            new(paths.SyncedPluginsDir, Recursive: false),
            new(paths.ProjectsDir, Recursive: false),
        };

        if (Directory.Exists(paths.ManagedDir))
        {
            targets.Add(new WatchTarget(paths.ManagedDir, Recursive: false));
            targets.Add(new WatchTarget(paths.ManagedSettingsDropIns, Recursive: false));
            targets.Add(new WatchTarget(Path.Combine(paths.ManagedClaudeDir, "skills"), Recursive: true));
        }

        if (OperatingSystem.IsMacOS())
        {
            targets.Add(new WatchTarget(ClaudeSettings.SystemPolicyStore.ProfilesFolder, Recursive: true));
        }

        foreach (var project in scan.Sources.Where(s => s.Kind == SourceKind.Project && s.Exists))
        {
            targets.Add(new WatchTarget(project.RootPath, Recursive: false));
            targets.Add(new WatchTarget(Path.Combine(project.RootPath, "skills"), Recursive: true));
            targets.Add(new WatchTarget(Path.Combine(project.RootPath, "commands"), Recursive: true));
        }

        foreach (var synced in scan.Sources.Where(s => s.Kind == SourceKind.Synced && s.Layout == SourceLayout.Standard))
        {
            targets.Add(new WatchTarget(Path.GetDirectoryName(synced.RootPath)!, Recursive: false));
            targets.Add(new WatchTarget(Path.Combine(synced.RootPath, "skills"), Recursive: true));
            targets.Add(new WatchTarget(Path.Combine(synced.RootPath, "commands"), Recursive: true));
        }

        return targets.Distinct().ToList();
    }
}

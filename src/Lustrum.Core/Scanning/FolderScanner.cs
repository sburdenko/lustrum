using System.Diagnostics;
using Lustrum.Core.Models;

namespace Lustrum.Core.Scanning;

/// <summary>
/// Reads the skills and commands kept in a folder that is not Claude Code's own, such as a repository checked in CI:
/// its .claude folders at any depth, plugins, folders whose skills/ holds skills, and the folder itself when it is one
/// skill or a collection of them. Commands count only in .claude folders and plugins, since a commands/ folder
/// elsewhere is more often documentation than Claude Code commands. Only the excluded folders and hidden folders other
/// than .claude are skipped, and no personal setting applies, so a repository gives the same result everywhere.
/// </summary>
public static class FolderScanner
{
    private const string SkillFile = "SKILL.md";

    public static ScanResult Scan(string folder)
    {
        var stopwatch = Stopwatch.StartNew();
        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
        var issues = new List<ScanIssue>();
        var sources = new List<SkillSource>();
        var skills = new List<Skill>();

        if (OwnSkillFiles(root, issues) is { Count: > 0 } own)
        {
            var collection = Source(root, root) with { Id = $"skills:{root}" };
            sources.Add(collection);
            Read(collection, SkillKind.Skill, own, skills, issues);
        }

        foreach (var (directory, withCommands) in Roots(root, issues))
        {
            var source = Source(root, directory);
            sources.Add(source);
            Read(source, SkillKind.Skill, SkillReader.SkillFiles(source, issues.Add), skills, issues);
            if (withCommands)
            {
                Read(source, SkillKind.Command, SkillReader.CommandFiles(source, issues.Add), skills, issues);
            }
        }

        var ordered = SkillTagger.Apply(skills, ScanOptions.Default)
            .OrderBy(s => s.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Source.Id, StringComparer.Ordinal)
            .ToList();
        stopwatch.Stop();
        return new ScanResult(sources, ordered, ConflictDetector.Detect(ordered), issues, DateTimeOffset.UtcNow, stopwatch.Elapsed);
    }

    // The folder itself as one skill, or as a collection of skill folders.
    private static IReadOnlyList<string> OwnSkillFiles(string root, List<ScanIssue> issues) =>
        File.Exists(Path.Combine(root, SkillFile))
            ? [Path.Combine(root, SkillFile)]
            : SafeFileSystem.Directories(root, issues.Add).Select(d => Path.Combine(d, SkillFile)).Where(File.Exists).ToList();

    private static IEnumerable<(string Directory, bool WithCommands)> Roots(string root, List<ScanIssue> issues)
    {
        var excluded = ScanOptions.Default.ExcludedFolders.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pending = new Queue<string>([root]);
        while (pending.TryDequeue(out var current))
        {
            var claude = Path.Combine(current, ".claude");
            if (Directory.Exists(claude) && IsRoot(claude, own: true, issues))
            {
                yield return (claude, true);
            }

            var own = Path.GetFileName(current) == ".claude" || File.Exists(Path.Combine(current, ".claude-plugin", "plugin.json"));
            if (IsRoot(current, own, issues))
            {
                yield return (current, own);
            }

            foreach (var child in SafeFileSystem.RealDirectories(current, issues.Add))
            {
                var name = Path.GetFileName(child);
                if (!excluded.Contains(name) && !name.StartsWith('.') && name is not ("skills" or "commands"))
                {
                    pending.Enqueue(child);
                }
            }
        }
    }

    private static bool IsRoot(string directory, bool own, List<ScanIssue> issues) =>
        SafeFileSystem.Directories(Path.Combine(directory, "skills"), issues.Add).Any(d => File.Exists(Path.Combine(d, SkillFile)))
        || (own && Directory.Exists(Path.Combine(directory, "commands")));

    private static SkillSource Source(string root, string directory)
    {
        var relative = Path.GetRelativePath(root, directory).Replace(Path.DirectorySeparatorChar, '/');
        var name = relative == "." ? Path.GetFileName(root) : relative;
        return new SkillSource($"folder:{directory}", SourceKind.Project, name, directory, null, true);
    }

    private static void Read(SkillSource source, SkillKind kind, IEnumerable<string> files, List<Skill> skills, List<ScanIssue> issues)
    {
        foreach (var file in files)
        {
            var (skill, issue) = SkillReader.Read(source, kind, file);
            skills.Add(skill);
            if (issue is not null)
            {
                issues.Add(issue);
            }
        }
    }
}

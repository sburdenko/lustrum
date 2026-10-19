using Lustrum.Core.Models;

namespace Lustrum.Core.Copying;

/// <summary>
/// Copies a skill folder or a command file into the personal or a project source. Nothing is overwritten: a
/// target that already has the folder, the file or a skill of the same name is refused.
/// </summary>
public static class SkillCopier
{
    public const int MaxFiles = 2000;
    public const long MaxBytes = 50L * 1024 * 1024;

    public static IReadOnlyList<SkillSource> Targets(ScanResult scan, Skill skill) =>
        scan.Sources
            .Where(s => s.Exists && s.Kind is SourceKind.Global or SourceKind.Project && s.Id != skill.Source.Id)
            .OrderBy(s => s.Kind)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();

    public static CopyPlan Plan(ScanResult scan, Skill skill, SkillSource target)
    {
        if (target.Kind is not (SourceKind.Global or SourceKind.Project) || !target.Exists)
        {
            return new BlockedCopy(skill, target, "Skills can only be copied to your personal skills or to a project");
        }

        if (target.Id == skill.Source.Id)
        {
            return new BlockedCopy(skill, target, "The skill is already there");
        }

        var from = skill.Kind == SkillKind.Skill ? Path.GetDirectoryName(skill.Path)! : skill.Path;
        var to = Destination(skill, target, from);
        if (Directory.Exists(to) || File.Exists(to))
        {
            return new BlockedCopy(skill, target, $"{Path.GetFileName(to)} already exists in {Describe(target)}");
        }

        var sameName = scan.Skills.FirstOrDefault(s => s.Source.Id == target.Id && s.Kind == skill.Kind && s.Name == skill.Name);
        return sameName is null
            ? new ReadyCopy(skill, target, from, to)
            : new BlockedCopy(skill, target, $"{Describe(target)} already has a {Noun(skill)} named {skill.Name}");
    }

    public static CopyResult Execute(ReadyCopy plan)
    {
        if (Directory.Exists(plan.To) || File.Exists(plan.To))
        {
            throw new SkillCopyException($"{plan.To} appeared in the meantime; nothing was copied");
        }

        try
        {
            return plan.IsFolder ? CopyFolder(plan.From, plan.To) : CopyFile(plan.From, plan.To);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new SkillCopyException($"Could not copy {plan.Skill.Name}: {ex.Message}", ex);
        }
    }

    private static string Destination(Skill skill, SkillSource target, string from)
    {
        if (skill.Kind == SkillKind.Skill)
        {
            return Path.Combine(target.RootPath, "skills", Path.GetFileName(from));
        }

        var relative = Path.GetRelativePath(Path.Combine(skill.Source.RootPath, "commands"), from);
        var outside = Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        return Path.Combine(target.RootPath, "commands", outside ? Path.GetFileName(from) : relative);
    }

    private static CopyResult CopyFile(string from, string to)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(to)!);
        File.Copy(from, to, overwrite: false);
        return new CopyResult(to, 1, []);
    }

    // Built in a staging folder next to skills/ (where neither Lustrum nor Claude Code looks for skills) and moved
    // into place at the end, so a failure or a rescan never sees half a skill.
    private static CopyResult CopyFolder(string from, string to)
    {
        var tree = new CopyTree();
        Collect(from, string.Empty, tree);
        var parent = Path.GetDirectoryName(to)!;
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(Path.GetDirectoryName(parent)!, $".lustrum-{Guid.NewGuid().ToString("N")[..8]}");
        try
        {
            foreach (var (source, relative) in tree.Files)
            {
                var destination = Path.Combine(staging, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                File.Copy(source, destination);
            }

            Directory.CreateDirectory(staging);
            Directory.Move(staging, to);
        }
        catch
        {
            TryDelete(staging);
            throw;
        }

        return new CopyResult(to, tree.Files.Count, tree.Skipped);
    }

    // Linked files and folders are left out: their content lives outside the skill and may not belong in a project.
    private static void Collect(string folder, string relative, CopyTree tree)
    {
        foreach (var file in Directory.EnumerateFiles(folder))
        {
            var info = new FileInfo(file);
            var fileRelative = Path.Combine(relative, info.Name);
            if (info.Name == ".git" || info.LinkTarget is not null)
            {
                tree.Skipped.Add(fileRelative);
                continue;
            }

            tree.Add(file, fileRelative, info.Length);
        }

        foreach (var directory in Directory.EnumerateDirectories(folder))
        {
            var name = Path.GetFileName(directory);
            var childRelative = Path.Combine(relative, name);
            if (name == ".git" || new DirectoryInfo(directory).LinkTarget is not null)
            {
                tree.Skipped.Add(childRelative);
                continue;
            }

            Collect(directory, childRelative, tree);
        }
    }

    private sealed class CopyTree
    {
        private long _bytes;

        public List<(string Source, string Relative)> Files { get; } = [];

        public List<string> Skipped { get; } = [];

        public void Add(string source, string relative, long length)
        {
            Files.Add((source, relative));
            _bytes += length;
            if (Files.Count > MaxFiles || _bytes > MaxBytes)
            {
                throw new SkillCopyException($"The skill folder holds more than {MaxFiles} files or {MaxBytes / 1024 / 1024} MB, more than Lustrum copies");
            }
        }
    }

    private static string Describe(SkillSource source) => source.Kind == SourceKind.Global ? "your personal skills" : source.DisplayName;

    private static string Noun(Skill skill) => skill.Kind == SkillKind.Command ? "command" : "skill";

    private static void TryDelete(string folder)
    {
        try
        {
            Directory.Delete(folder, recursive: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}

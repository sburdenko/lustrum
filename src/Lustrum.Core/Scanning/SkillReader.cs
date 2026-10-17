using Lustrum.Core.Models;
using Lustrum.Core.Parsing;

namespace Lustrum.Core.Scanning;

internal static class SkillReader
{
    public const int HeadBytes = 64 * 1024;
    public const int MaxDescriptionLength = 1024;
    private const int MaxListedFiles = 200;
    private const int MaxScannedFiles = 1000;
    private const int MaxSiblingDepth = 4;
    private const int MaxCommandDepth = 3;
    private const string SkillFileName = "SKILL.md";

    public static IEnumerable<string> SkillFiles(SkillSource source, Action<ScanIssue> report)
    {
        var folders = source.Layout == SourceLayout.Flat
            ? SyncedDiscovery.Visible(source.RootPath, report)
            : SafeFileSystem.Directories(Path.Combine(source.RootPath, "skills"), report);
        return folders.Select(dir => Path.Combine(dir, SkillFileName)).Where(File.Exists);
    }

    public static IEnumerable<string> CommandFiles(SkillSource source, Action<ScanIssue> report)
    {
        if (source.Layout == SourceLayout.Flat)
        {
            return [];
        }

        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = MaxCommandDepth,
            IgnoreInaccessible = true,
        };

        return SafeFileSystem.Files(Path.Combine(source.RootPath, "commands"), "*.md", options, report);
    }

    public static (Skill Skill, ScanIssue? Issue) Read(SkillSource source, SkillKind kind, string path)
    {
        var fallbackName = kind == SkillKind.Skill
            ? Path.GetFileName(Path.GetDirectoryName(path))!
            : CommandName(source, path);

        try
        {
            var info = new FileInfo(path);
            var parsed = FrontmatterParser.Parse(ReadHead(path));
            return Build(source, kind, path, fallbackName, info, parsed);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Unreadable(source, kind, path, fallbackName, $"Cannot read file: {ex.Message}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return Unreadable(source, kind, path, fallbackName, $"Unexpected error while reading: {ex.Message}");
        }
    }

    private static (Skill, ScanIssue?) Unreadable(SkillSource source, SkillKind kind, string path, string fallbackName, string message)
    {
        var broken = Create(source, kind, path, fallbackName, null, FrontmatterResult.Missing(string.Empty), 0, DateTimeOffset.MinValue, Health.ParseError);
        return (broken, new ScanIssue(path, message));
    }

    private static (Skill, ScanIssue?) Build(SkillSource source, SkillKind kind, string path, string fallbackName, FileInfo info, FrontmatterResult parsed)
    {
        var name = kind == SkillKind.Skill && parsed.Fields.TryGetValue("name", out var declared) && declared.Length > 0
            ? declared
            : fallbackName;
        var description = ResolveDescription(parsed, kind);
        var health = Evaluate(kind, parsed, description);
        var skill = Create(source, kind, path, name, description, parsed, info.Length, info.LastWriteTimeUtc, health);
        var issue = parsed.State == FrontmatterState.Invalid ? new ScanIssue(path, parsed.Error ?? "Invalid frontmatter") : null;
        return (skill, issue);
    }

    private static string? ResolveDescription(FrontmatterResult parsed, SkillKind kind)
    {
        if (parsed.Fields.TryGetValue("description", out var description) && description.Length > 0)
        {
            return description;
        }

        var useBody = parsed.State == FrontmatterState.Missing || kind == SkillKind.Command;
        return useBody ? FirstNonEmptyLine(parsed.Body) : null;
    }

    private static Health Evaluate(SkillKind kind, FrontmatterResult parsed, string? description)
    {
        if (parsed.State == FrontmatterState.Invalid)
        {
            return Health.ParseError;
        }

        if (kind == SkillKind.Skill)
        {
            if (parsed.State == FrontmatterState.Missing)
            {
                return Health.NoFrontmatter;
            }

            if (!parsed.Fields.TryGetValue("name", out var name) || name.Length == 0)
            {
                return Health.MissingName;
            }

            if (!parsed.Fields.ContainsKey("description") || parsed.Fields["description"].Length == 0)
            {
                return Health.MissingDescription;
            }
        }

        return description?.Length > MaxDescriptionLength ? Health.DescriptionTooLong : Health.Ok;
    }

    private static Skill Create(
        SkillSource source,
        SkillKind kind,
        string path,
        string name,
        string? description,
        FrontmatterResult parsed,
        long size,
        DateTimeOffset modified,
        Health health)
    {
        var prefix = source.Namespace;
        return new Skill(
            StableId(source, path),
            name,
            prefix is null ? name : $"{prefix}:{name}",
            description,
            kind,
            source,
            path,
            null,
            parsed.Fields,
            parsed.AllowedTools,
            kind == SkillKind.Skill ? SiblingFiles(path) : [],
            size,
            modified,
            health,
            []);
    }

    private static string CommandName(SkillSource source, string path)
    {
        var relative = Path.GetRelativePath(Path.Combine(source.RootPath, "commands"), path);
        var withoutExtension = relative[..^Path.GetExtension(relative).Length];
        return withoutExtension.Replace(Path.DirectorySeparatorChar, ':').Replace(Path.AltDirectorySeparatorChar, ':');
    }

    private static string StableId(SkillSource source, string path)
    {
        var relative = Path.GetRelativePath(source.RootPath, path).Replace('\\', '/');
        return $"{source.Id}#{relative}";
    }

    private static string ReadHead(string path)
    {
        using var stream = SharedFile.OpenRead(path);
        var buffer = new byte[HeadBytes];
        var read = stream.Read(buffer, 0, buffer.Length);
        return System.Text.Encoding.UTF8.GetString(buffer, 0, read);
    }

    private static string? FirstNonEmptyLine(string text) =>
        text.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);

    private static IReadOnlyList<string> SiblingFiles(string skillPath)
    {
        var folder = Path.GetDirectoryName(skillPath)!;
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            MaxRecursionDepth = MaxSiblingDepth,
            IgnoreInaccessible = true,
        };

        try
        {
            return Directory.EnumerateFiles(folder, "*", options)
                .Take(MaxScannedFiles)
                .Select(f => Path.GetRelativePath(folder, f))
                .Where(f => f != SkillFileName)
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
                .Take(MaxListedFiles)
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}

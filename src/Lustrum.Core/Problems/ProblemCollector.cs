using Lustrum.Core.Models;

namespace Lustrum.Core.Problems;

public static class ProblemCollector
{
    public static IReadOnlyList<Problem> Collect(ScanResult scan)
    {
        var issuesByPath = scan.Issues
            .GroupBy(i => i.Path, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First().Message, StringComparer.Ordinal);
        var skillPaths = scan.Skills.Select(s => s.Path).ToHashSet(StringComparer.Ordinal);

        return BrokenSkills(scan, issuesByPath)
            .Concat(LongDescriptions(scan))
            .Concat(MissingProjects(scan))
            .Concat(ReadIssues(scan, skillPaths))
            .ToList();
    }

    private static IEnumerable<Problem> BrokenSkills(ScanResult scan, Dictionary<string, string> issuesByPath) =>
        scan.Skills
            .Where(s => s.IsBroken)
            .OrderBy(s => s.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(s => new Problem(ProblemKind.BrokenSkill, s.InvocationName, s.Path, issuesByPath.GetValueOrDefault(s.Path), s));

    private static IEnumerable<Problem> LongDescriptions(ScanResult scan) =>
        scan.Skills
            .Where(s => s.Health == Health.DescriptionTooLong)
            .OrderBy(s => s.FullName, StringComparer.OrdinalIgnoreCase)
            .Select(s => new Problem(ProblemKind.LongDescription, s.InvocationName, s.Path, $"{s.Description?.Length ?? 0} characters", s));

    private static IEnumerable<Problem> MissingProjects(ScanResult scan) =>
        scan.Sources
            .Where(s => s.Kind == SourceKind.Project && !s.Exists)
            .OrderBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(s => new Problem(ProblemKind.MissingProject, s.DisplayName, s.RootPath, null, null));

    private static IEnumerable<Problem> ReadIssues(ScanResult scan, HashSet<string> skillPaths) =>
        scan.Issues
            .Where(i => !skillPaths.Contains(i.Path))
            .Select(i => new Problem(ProblemKind.ReadIssue, System.IO.Path.GetFileName(i.Path), i.Path, i.Message, null));
}

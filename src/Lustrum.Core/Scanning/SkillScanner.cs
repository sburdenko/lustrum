using System.Collections.Concurrent;
using System.Diagnostics;
using Lustrum.Core.Models;

namespace Lustrum.Core.Scanning;

public static class SkillScanner
{
    public static async Task<ScanResult> ScanAsync(
        ClaudePaths paths,
        ScanOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var settings = options ?? ScanOptions.Default;
        var discovered = SourceDiscovery.Discover(paths, settings);

        var skills = new ConcurrentBag<Skill>();
        var issues = new ConcurrentBag<ScanIssue>(discovered.Issues);
        var parallelism = new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = cancellationToken,
        };

        await Parallel.ForEachAsync(discovered.Sources, parallelism, (source, _) =>
        {
            ScanSource(source, skills, issues);
            return ValueTask.CompletedTask;
        });

        var tagged = SkillTagger.Apply(skills.ToList(), settings);
        var ordered = tagged
            .OrderBy(s => s.FullName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.Source.Id, StringComparer.Ordinal)
            .ToList();

        stopwatch.Stop();
        return new ScanResult(
            OrderSources(discovered.Sources),
            ordered,
            ConflictDetector.Detect(ordered),
            issues.OrderBy(i => i.Path, StringComparer.Ordinal).ToList(),
            DateTimeOffset.UtcNow,
            stopwatch.Elapsed);
    }

    private static void ScanSource(SkillSource source, ConcurrentBag<Skill> skills, ConcurrentBag<ScanIssue> issues)
    {
        if (!source.Exists)
        {
            return;
        }

        Collect(source, SkillKind.Skill, SkillReader.SkillFiles(source, issues.Add), skills, issues);
        Collect(source, SkillKind.Command, SkillReader.CommandFiles(source, issues.Add), skills, issues);
    }

    private static void Collect(
        SkillSource source,
        SkillKind kind,
        IEnumerable<string> files,
        ConcurrentBag<Skill> skills,
        ConcurrentBag<ScanIssue> issues)
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

    private static IReadOnlyList<SkillSource> OrderSources(IReadOnlyList<SkillSource> sources) =>
        sources
            .OrderBy(s => s.Kind)
            .ThenBy(s => s.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
}

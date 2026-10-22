using Lustrum.Core.Effective;
using Lustrum.Core.Models;

namespace Lustrum.Core.Usage;

/// <summary>
/// Splits the use of a name among the copies that share it. Transcripts record only the name a session invoked and
/// the folder it ran in, so each use goes to the copy Claude Code resolves in that folder by today's files and
/// settings. Either may have changed since, so for shared names the split is an estimate; a name with one copy keeps
/// its exact count. A use no copy can be named for, such as one in a folder where every copy loads only on demand, is
/// counted as unattributed rather than guessed.
/// </summary>
public sealed class CopyUsage
{
    private readonly UsageReport _report;
    private readonly HashSet<string> _shared;
    private readonly IReadOnlyDictionary<string, SkillUsage> _byCopy;
    private readonly IReadOnlyDictionary<string, int> _unattributed;

    private CopyUsage(UsageReport report, HashSet<string> shared, IReadOnlyDictionary<string, SkillUsage> byCopy, IReadOnlyDictionary<string, int> unattributed)
    {
        _report = report;
        _shared = shared;
        _byCopy = byCopy;
        _unattributed = unattributed;
    }

    /// <summary>
    /// What the split depends on besides the transcripts: the copies of shared names and the sources. Two scans with
    /// the same signature split usage the same way.
    /// </summary>
    public static string Signature(ScanResult scan) =>
        string.Join(
            '\n',
            scan.Skills.GroupBy(s => s.FullName, StringComparer.Ordinal).Where(g => g.Count() > 1).SelectMany(g => g).Select(s => s.Id)
                .Concat(scan.Sources.Select(s => $"{s.Id}|{s.Exists}|{s.IsEnabled}"))
                .Order(StringComparer.Ordinal));

    public static CopyUsage Attribute(ScanResult scan, UsageReport report)
    {
        var copies = scan.Skills
            .GroupBy(s => s.FullName, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
        var resolver = new Resolver(scan);
        var attributed = report.Invocations
            .Where(i => copies.ContainsKey(i.Name))
            .Select(i => (Use: i, Copy: resolver.Winner(i.ProjectPath, copies[i.Name])))
            .ToList();
        var byCopy = attributed
            .Where(p => p.Copy is not null)
            .GroupBy(p => p.Copy!.Id, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => UsageCollector.Summarize(g.First().Use.Name, g.Select(p => p.Use).ToList()), StringComparer.Ordinal);
        var unattributed = attributed
            .Where(p => p.Copy is null)
            .GroupBy(p => p.Use.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        return new CopyUsage(report, copies.Keys.ToHashSet(StringComparer.Ordinal), byCopy, unattributed);
    }

    /// <summary>The uses of this copy, or null when none was recorded.</summary>
    public SkillUsage? For(Skill skill) =>
        IsEstimate(skill) ? _byCopy.GetValueOrDefault(skill.Id) : _report.For(skill.FullName);

    /// <summary>Every recorded use of the skill's name, whichever copy ran.</summary>
    public SkillUsage? ForName(Skill skill) => _report.For(skill.FullName);

    /// <summary>Whether several copies share the name, so <see cref="For"/> is an estimate.</summary>
    public bool IsEstimate(Skill skill) => _shared.Contains(skill.FullName);

    /// <summary>Uses of the skill's name that could not be given to any one copy.</summary>
    public int Unattributed(Skill skill) => _unattributed.GetValueOrDefault(skill.FullName);

    /// <summary>No recorded use ran this copy, and none of the name's uses is left that might have.</summary>
    public bool IsUnused(Skill skill) => For(skill) is null && Unattributed(skill) == 0;

    private sealed class Resolver(ScanResult scan)
    {
        private readonly Dictionary<string, SkillSource?> _projects = new(StringComparer.Ordinal);
        private readonly Dictionary<string, EffectiveSet> _sets = new(StringComparer.Ordinal);

        public Skill? Winner(string? folder, IReadOnlyList<Skill> copies)
        {
            if (folder is not null && Project(folder) is { } project)
            {
                var set = Set(project);
                var active = copies.Where(c => set.Find(c.Id) is { Availability: Availability.Active, IsTurnedOff: false }).ToList();
                return active.FirstOrDefault(c => c.Source.Id == project.Id) ?? active.FirstOrDefault();
            }

            return copies.Where(c => c.Source.Kind != SourceKind.Project).OrderBy(EffectiveSetBuilder.Rank).FirstOrDefault();
        }

        private SkillSource? Project(string folder)
        {
            if (!_projects.TryGetValue(folder, out var project))
            {
                project = Nearest(folder);
                _projects[folder] = project;
            }

            return project;
        }

        private SkillSource? Nearest(string folder)
        {
            for (var current = folder; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
            {
                if (EffectiveSetBuilder.FindProject(scan, current) is { } project)
                {
                    return project;
                }
            }

            return null;
        }

        private EffectiveSet Set(SkillSource project)
        {
            if (!_sets.TryGetValue(project.Id, out var set))
            {
                set = EffectiveSetBuilder.Build(scan, project);
                _sets[project.Id] = set;
            }

            return set;
        }
    }
}

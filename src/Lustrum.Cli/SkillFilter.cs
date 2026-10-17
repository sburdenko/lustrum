using Lustrum.Core.Indexing;
using Lustrum.Core.Models;

namespace Lustrum.Cli;

internal static class SkillFilter
{
    public static readonly IReadOnlyDictionary<string, SourceKind> SourceNames =
        new Dictionary<string, SourceKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["global"] = SourceKind.Global,
            ["project"] = SourceKind.Project,
            ["plugin"] = SourceKind.Plugin,
            ["synced"] = SourceKind.Synced,
        };

    public static IEnumerable<Skill> Apply(IEnumerable<Skill> skills, SourceKind? source, bool brokenOnly) =>
        skills
            .Where(s => source is null || s.Source.Kind == source)
            .Where(s => !brokenOnly || SkillScope.Problems.Includes(s));
}

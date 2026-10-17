using Lustrum.Core.Models;

namespace Lustrum.Core.Scanning;

internal static class SkillTagger
{
    public const string Plugin = "plugin";
    public const string Synced = "synced";
    public const string Command = "command";
    public const string Broken = "broken";
    public const string Duplicate = "duplicate";

    public static IReadOnlyList<Skill> Apply(IReadOnlyList<Skill> skills, ScanOptions options)
    {
        var duplicated = DuplicatedNames(skills);
        return skills.Select(skill => skill with { Tags = TagsFor(skill, options, duplicated) }).ToList();
    }

    public static HashSet<string> DuplicatedNames(IReadOnlyList<Skill> skills) =>
        skills
            .GroupBy(s => s.ConflictKey, StringComparer.Ordinal)
            .Where(g => g.Select(s => s.Source.Id).Distinct().Count() > 1)
            .Select(g => g.Key)
            .ToHashSet(StringComparer.Ordinal);

    private static IReadOnlyList<string> TagsFor(Skill skill, ScanOptions options, HashSet<string> duplicated)
    {
        var tags = new List<string>();
        tags.AddRange(options.OwnPrefixes.Where(p => skill.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase)));
        AddIf(tags, skill.Source.Kind == SourceKind.Plugin, Plugin);
        AddIf(tags, skill.Source.Kind == SourceKind.Synced, Synced);
        AddIf(tags, skill.Kind == SkillKind.Command, Command);
        AddIf(tags, skill.IsBroken, Broken);
        AddIf(tags, duplicated.Contains(skill.ConflictKey), Duplicate);
        return tags;
    }

    private static void AddIf(List<string> tags, bool condition, string tag)
    {
        if (condition)
        {
            tags.Add(tag);
        }
    }
}

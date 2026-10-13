using Lustrum.Core.Models;

namespace Lustrum.App.Tests;

internal static class TestData
{
    public static readonly SkillSource Global =
        new("global", SourceKind.Global, "Global", "/home/u/.claude", null, true);

    public static readonly SkillSource Project =
        new("project:/dev/app", SourceKind.Project, "app", "/dev/app/.claude", null, true);

    public static readonly SkillSource EmptyProject =
        new("project:/dev/empty", SourceKind.Project, "empty", "/dev/empty/.claude", null, true);

    public static readonly SkillSource GhostProject =
        new("project:/dev/gone", SourceKind.Project, "gone", "/dev/gone", null, false);

    public static readonly SkillSource Plugin =
        new("plugin:market/unity", SourceKind.Plugin, "unity (market)", "/plugins/unity", null, true);

    public static Skill Skill(SkillSource source, string name, string description = "desc", Health health = Health.Ok)
    {
        var fullName = source.Kind == SourceKind.Plugin ? $"unity:{name}" : name;
        return new Skill(
            $"{source.Id}#{name}",
            name,
            fullName,
            description,
            SkillKind.Skill,
            source,
            $"/missing/{source.Id.Replace(':', '_').Replace('/', '_')}/{name}/SKILL.md",
            null,
            new Dictionary<string, string> { ["name"] = name, ["description"] = description },
            [],
            [],
            10,
            DateTimeOffset.UnixEpoch,
            health,
            []);
    }

    public static ScanResult Scan(IReadOnlyList<SkillSource> sources, IReadOnlyList<Skill> skills) =>
        new(sources, skills, [], [], DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(5));

    public static ScanResult Default() => Scan(
        [Global, Project, EmptyProject, GhostProject, Plugin],
        [
            Skill(Global, "design", "Visual design"),
            Skill(Global, "broken-one", "x", Health.MissingName),
            Skill(Project, "deploy", "Ship it"),
            Skill(Plugin, "ui", "Unity UI"),
        ]);
}

using Lustrum.Core.ClaudeSettings;

namespace Lustrum.Core.Models;

public sealed record ScanResult(
    IReadOnlyList<SkillSource> Sources,
    IReadOnlyList<Skill> Skills,
    IReadOnlyList<Conflict> Conflicts,
    IReadOnlyList<ScanIssue> Issues,
    DateTimeOffset ScannedAt,
    TimeSpan Duration)
{
    public SettingsLayers Settings { get; init; } = SettingsLayers.Empty;
}

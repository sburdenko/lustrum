using Lustrum.Core.Models;

namespace Lustrum.Core.Usage;

public sealed record UsageReport(
    IReadOnlyDictionary<string, SkillUsage> ByName,
    int TranscriptCount,
    long BytesRead,
    IReadOnlyList<ScanIssue> Issues,
    TimeSpan Duration)
{
    public static UsageReport Empty { get; } = new(new Dictionary<string, SkillUsage>(), 0, 0, [], TimeSpan.Zero);

    public SkillUsage? For(string fullName) => ByName.GetValueOrDefault(fullName);
}

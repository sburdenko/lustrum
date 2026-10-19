namespace Lustrum.Core.Usage;

public sealed record SkillUsage(
    string Name,
    int Count,
    DateTimeOffset LastUsed,
    IReadOnlyList<string> Projects,
    int ByModel,
    int ByUser,
    IReadOnlyList<DateTimeOffset>? UsedAt = null)
{
    /// <summary>Every recorded use, oldest first.</summary>
    public IReadOnlyList<DateTimeOffset> Times => UsedAt ?? [];

    /// <summary>How many uses fell on each of the last <paramref name="days"/> local days, oldest first.</summary>
    public IReadOnlyList<int> PerDay(DateOnly today, int days, TimeZoneInfo zone)
    {
        var first = today.AddDays(1 - days);
        var counts = new int[days];
        foreach (var time in Times)
        {
            var day = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(time, zone).DateTime);
            var index = day.DayNumber - first.DayNumber;
            if (index >= 0 && index < days)
            {
                counts[index]++;
            }
        }

        return counts;
    }
}

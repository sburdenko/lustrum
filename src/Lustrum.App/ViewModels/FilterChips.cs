namespace Lustrum.App.ViewModels;

public static class FilterChips
{
    private static readonly string[] FixedTags = ["plugin", "command", "broken", "duplicate"];

    public static IReadOnlyList<ChipViewModel> Build(IReadOnlyList<string> ownPrefixes, IEnumerable<string> activeLabels, bool canJudgeUnused)
    {
        var active = activeLabels.ToHashSet(StringComparer.Ordinal);
        return ownPrefixes
            .Select(prefix => new ChipViewModel($"{prefix}*", new TagChip(prefix)))
            .Concat(FixedTags.Select(tag => new ChipViewModel(tag, new TagChip(tag))))
            .Append(new ChipViewModel("unused", new UnusedChip(), "Skills with no recorded use in Claude Code transcripts"))
            .Select(chip =>
            {
                chip.IsOn = active.Contains(chip.Label);
                chip.IsAvailable = canJudgeUnused || !chip.Filter.NeedsUsage;
                return chip;
            })
            .ToList();
    }
}

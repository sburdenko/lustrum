namespace Lustrum.App.ViewModels;

public static class FilterChips
{
    private static readonly string[] FixedTags = ["plugin", "synced", "command", "broken", "duplicate"];

    public static IReadOnlyList<ChipViewModel> Build(IReadOnlyList<string> ownPrefixes, IEnumerable<string> activeLabels, ChipContext context)
    {
        var active = activeLabels.ToHashSet(StringComparer.Ordinal);
        return ownPrefixes
            .Select(prefix => new ChipViewModel($"{prefix}*", new TagChip(prefix)))
            .Concat(FixedTags.Select(tag => new ChipViewModel(tag, new TagChip(tag))))
            .Append(new ChipViewModel("unused", new UnusedChip(), "Skills with no recorded use in Claude Code transcripts"))
            .Append(new ChipViewModel("updates", new UpdatesChip(), "Skills whose git repository is behind its upstream; run Check for updates first"))
            .Select(chip =>
            {
                chip.IsOn = active.Contains(chip.Label);
                chip.IsAvailable = chip.Filter.IsAvailable(context);
                return chip;
            })
            .ToList();
    }
}

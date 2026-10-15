namespace Lustrum.App.ViewModels;

public static class FilterChips
{
    private static readonly string[] FixedTags = ["plugin", "command", "broken", "duplicate"];

    public static IReadOnlyList<ChipViewModel> Build(IReadOnlyList<string> ownPrefixes, IEnumerable<string> activeTags)
    {
        var active = activeTags.ToHashSet(StringComparer.Ordinal);
        return ownPrefixes
            .Select(prefix => new ChipViewModel($"{prefix}*", prefix))
            .Concat(FixedTags.Select(tag => new ChipViewModel(tag, tag)))
            .Select(chip =>
            {
                chip.IsOn = active.Contains(chip.Tag);
                return chip;
            })
            .ToList();
    }
}

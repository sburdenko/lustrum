using System.Globalization;
using Lustrum.Core.Comparing;

namespace Lustrum.App.ViewModels;

public enum DiffRowKind { Same, OnlyHere, OnlyThere, Folded }

public sealed record DiffRowViewModel(DiffRowKind Kind, string Prefix, string Text, string Number)
{
    public bool IsOnlyHere => Kind == DiffRowKind.OnlyHere;

    public bool IsOnlyThere => Kind == DiffRowKind.OnlyThere;

    public bool IsFolded => Kind == DiffRowKind.Folded;

    public static DiffRowViewModel From(DiffRow row) => row switch
    {
        DiffGap gap => Folded($"⋯ {gap.Hidden} unchanged {(gap.Hidden == 1 ? "line" : "lines")}"),
        DiffLine { Kind: DiffKind.Removed } line => new(DiffRowKind.OnlyHere, "-", line.Text, Format(line.LeftNumber)),
        DiffLine { Kind: DiffKind.Added } line => new(DiffRowKind.OnlyThere, "+", line.Text, Format(line.RightNumber)),
        DiffLine line => new(DiffRowKind.Same, " ", line.Text, Format(line.LeftNumber)),
        _ => throw new ArgumentOutOfRangeException(nameof(row)),
    };

    public static DiffRowViewModel Folded(string text) => new(DiffRowKind.Folded, " ", text, string.Empty);

    private static string Format(int? number) => number?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}

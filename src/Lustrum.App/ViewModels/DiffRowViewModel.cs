using System.Globalization;
using Lustrum.Core.Comparing;

namespace Lustrum.App.ViewModels;

public sealed record DiffRowViewModel(string Prefix, string Text, string Number, bool IsOnlyHere, bool IsOnlyThere, bool IsGap)
{
    public static DiffRowViewModel From(DiffRow row) => row switch
    {
        DiffGap gap => new(" ", $"⋯ {gap.Hidden} unchanged {(gap.Hidden == 1 ? "line" : "lines")}", string.Empty, false, false, true),
        DiffLine { Kind: DiffKind.Removed } line => new("-", line.Text, Format(line.LeftNumber), true, false, false),
        DiffLine { Kind: DiffKind.Added } line => new("+", line.Text, Format(line.RightNumber), false, true, false),
        DiffLine line => new(" ", line.Text, Format(line.LeftNumber), false, false, false),
        _ => throw new ArgumentOutOfRangeException(nameof(row)),
    };

    private static string Format(int? number) => number?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
}

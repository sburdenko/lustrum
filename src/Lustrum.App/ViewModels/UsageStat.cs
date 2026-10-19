namespace Lustrum.App.ViewModels;

public sealed record UsageStat(string Label, string Value, bool IsKnown, bool IsLast);

/// <summary>One day of the card's 30-day strip.</summary>
public sealed record UsageDay(bool IsUsed, bool IsLast);

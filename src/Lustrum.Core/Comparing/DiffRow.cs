namespace Lustrum.Core.Comparing;

public enum DiffKind { Same, Added, Removed }

public abstract record DiffRow;

public sealed record DiffLine(DiffKind Kind, string Text, int? LeftNumber, int? RightNumber) : DiffRow;

public sealed record DiffGap(int Hidden) : DiffRow;

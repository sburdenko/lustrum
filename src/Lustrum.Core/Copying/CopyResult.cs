namespace Lustrum.Core.Copying;

/// <summary>Skipped lists linked folders and nested repositories that were left out of the copy.</summary>
public sealed record CopyResult(string Destination, int FileCount, IReadOnlyList<string> Skipped);

namespace Lustrum.Core.ClaudeSettings;

/// <summary>One OS-level policy document: its JSON text, or the reason it could not be read.</summary>
public sealed record PolicyText(string Label, string? Json, string? Error = null);

namespace Lustrum.Core.Git;

/// <summary>An uncommitted change; Path is relative to the repository root and uses forward slashes.</summary>
public sealed record GitChange(string Path, GitChangeKind Kind);

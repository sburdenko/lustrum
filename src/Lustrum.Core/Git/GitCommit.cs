namespace Lustrum.Core.Git;

public sealed record GitCommit(string Hash, string ShortHash, DateTimeOffset When, string Author, string Subject);

namespace Lustrum.Core.Git;

public class GitException(string message) : Exception(message);

public sealed class GitUnavailableException(string message) : GitException(message);

public sealed class GitUpdateRefusedException(string reason) : GitException(reason);

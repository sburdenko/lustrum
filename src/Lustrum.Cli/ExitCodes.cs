namespace Lustrum.Cli;

internal static class ExitCodes
{
    /// <summary>The command ran and found something to report: problems, or a copy that could not be read.</summary>
    public const int Found = 1;

    /// <summary>Bad input: an unknown option value, a missing folder, nothing to work on.</summary>
    public const int Usage = 2;
}

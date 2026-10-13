namespace Lustrum.Core.Parsing;

public enum FrontmatterState { Missing, Parsed, Invalid }

public sealed record FrontmatterResult(
    FrontmatterState State,
    IReadOnlyDictionary<string, string> Fields,
    IReadOnlyList<string> AllowedTools,
    string Body,
    string? Error)
{
    public static FrontmatterResult Missing(string body) =>
        new(FrontmatterState.Missing, EmptyFields, [], body, null);

    public static FrontmatterResult Invalid(string body, string error) =>
        new(FrontmatterState.Invalid, EmptyFields, [], body, error);

    private static readonly IReadOnlyDictionary<string, string> EmptyFields =
        new Dictionary<string, string>();
}

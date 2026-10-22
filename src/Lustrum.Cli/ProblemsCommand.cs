using System.CommandLine;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lustrum.Core.Problems;

namespace Lustrum.Cli;

/// <summary>
/// Prints every problem the window's Problems list shows, or those of the skills in one folder; exits with 1 when
/// there is any, for scripts and CI.
/// </summary>
internal static class ProblemsCommand
{

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static Command Create()
    {
        var json = new Option<bool>("--json") { Description = "Emit machine-readable JSON" };
        var folder = new Argument<string?>("folder")
        {
            Arity = ArgumentArity.ZeroOrOne,
            Description = "Check only the skills and commands in this folder, such as a repository in CI: its .claude folders, plugins, skills/ collections, or the folder itself when it is one skill",
        };
        var command = new Command("problems", "List broken skills, missing linked files, allowed-tools issues, long descriptions and missing projects");
        command.Options.Add(json);
        command.Arguments.Add(folder);
        command.SetAction(async (parseResult, cancellationToken) =>
            await RunAsync(parseResult.GetValue(folder), parseResult.GetValue(json), cancellationToken));
        return command;
    }

    private static async Task<int> RunAsync(string? folder, bool asJson, CancellationToken cancellationToken)
    {
        if (folder is not null && !Directory.Exists(folder))
        {
            await Console.Error.WriteLineAsync($"No folder at '{folder}'.");
            return ExitCodes.Usage;
        }

        var scan = folder is null ? await CliScan.RunAsync(null, cancellationToken) : await CliScan.RunFolderAsync(folder, cancellationToken);
        if (folder is not null && scan.Skills.Count == 0)
        {
            await Console.Error.WriteLineAsync($"No skills or commands found in '{folder}'.");
            return ExitCodes.Usage;
        }

        var problems = ProblemCollector.Collect(scan);
        Console.WriteLine(asJson
            ? JsonSerializer.Serialize(problems.Select(p => new { p.Kind, p.Subject, Message = Message(p), p.Path }), JsonOptions)
            : Render(problems));
        return problems.Count == 0 ? 0 : ExitCodes.Found;
    }

    public static string Render(IReadOnlyList<Problem> problems)
    {
        if (problems.Count == 0)
        {
            return "No problems found.";
        }

        var kindWidth = Math.Max(4, problems.Max(p => Label(p.Kind).Length));
        var subjectWidth = Math.Max(7, problems.Max(p => p.Subject.Length));
        var builder = new StringBuilder();
        builder.AppendLine($"{"KIND".PadRight(kindWidth)}  {"SUBJECT".PadRight(subjectWidth)}  MESSAGE");
        foreach (var problem in problems)
        {
            builder.AppendLine($"{Label(problem.Kind).PadRight(kindWidth)}  {problem.Subject.PadRight(subjectWidth)}  {Message(problem)}");
        }

        builder.AppendLine().Append(CultureInfo.InvariantCulture, $"{problems.Count} {(problems.Count == 1 ? "problem" : "problems")}");
        return builder.ToString();
    }

    public static string Message(Problem problem) => problem.Kind switch
    {
        ProblemKind.BrokenSkill => problem.Detail is null ? problem.Skill!.Health.ToString() : $"{problem.Skill!.Health}: {problem.Detail}",
        ProblemKind.LongDescription => $"Description is over 1024 characters ({problem.Detail})",
        ProblemKind.MissingProject => $"Project folder no longer exists: {problem.Path}",
        ProblemKind.MissingReference or ProblemKind.ToolEntry or ProblemKind.BroadPermission => problem.Detail ?? problem.Kind.ToString(),
        _ => problem.Detail ?? "Could not be read",
    };

    private static string Label(ProblemKind kind) => kind switch
    {
        ProblemKind.BrokenSkill => "broken",
        ProblemKind.LongDescription => "long",
        ProblemKind.MissingProject => "gone",
        ProblemKind.ReadIssue => "read",
        ProblemKind.MissingReference => "link",
        ProblemKind.ToolEntry => "tool",
        ProblemKind.BroadPermission => "wide",
        _ => kind.ToString().ToLowerInvariant(),
    };
}

using System.CommandLine;
using Lustrum.Core.Scanning;
using Lustrum.Core.Settings;

namespace Lustrum.Cli;

internal static class DiffCommand
{
    private const int UsageError = 2;

    public static Command Create()
    {
        var name = new Argument<string>("name") { Description = "Skill or command name, with plugin prefix if it has one" };
        var command = new Command("diff", "Compare the copies of a skill that exist under the same name");
        command.Arguments.Add(name);
        command.SetAction(async (parseResult, cancellationToken) => await RunAsync(parseResult.GetValue(name)!, cancellationToken));
        return command;
    }

    private static async Task<int> RunAsync(string name, CancellationToken cancellationToken)
    {
        var settings = SettingsStore.Load(SettingsStore.DefaultPath).Settings;
        var scan = await SkillScanner.ScanAsync(ClaudePaths.ForCurrentUser(), settings.ToScanOptions(), cancellationToken);
        var wanted = name.TrimStart('/');
        var copies = scan.Skills.Where(s => s.FullName == wanted).ToList();
        if (copies.Count == 0)
        {
            await Console.Error.WriteLineAsync($"No skill named '{wanted}'.");
            return UsageError;
        }

        Console.WriteLine(DiffReport.Render(copies, SkillBodyReader.ReadText));
        return 0;
    }
}

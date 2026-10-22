using System.CommandLine;
using Lustrum.Cli;

var root = new RootCommand("Lustrum: inventory of Claude Code skills on this machine");
root.Subcommands.Add(ListCommand.Create());
root.Subcommands.Add(DiffCommand.Create());
root.Subcommands.Add(ProblemsCommand.Create());
return await root.Parse(args).InvokeAsync();

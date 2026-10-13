using System.CommandLine;
using Lustrum.Cli;

var root = new RootCommand("Lustrum: inventory of Claude Code skills on this machine");
root.Subcommands.Add(ListCommand.Create());
return await root.Parse(args).InvokeAsync();

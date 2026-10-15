using FluentAssertions;
using Lustrum.App.Services;

namespace Lustrum.App.Tests;

public class CommandLineTests
{
    [Theory]
    [InlineData("code", new[] { "code" })]
    [InlineData("code --wait", new[] { "code", "--wait" })]
    [InlineData("\"C:\\Program Files\\VS Code\\code.exe\" -g", new[] { "C:\\Program Files\\VS Code\\code.exe", "-g" })]
    [InlineData("  open   -a  \"Visual Studio Code\" ", new[] { "open", "-a", "Visual Studio Code" })]
    [InlineData("", new string[0])]
    public void Splits_on_spaces_outside_quotes(string command, string[] expected) =>
        CommandLine.Split(command).Should().Equal(expected);
}

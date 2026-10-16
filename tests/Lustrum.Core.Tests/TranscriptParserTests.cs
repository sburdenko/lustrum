using FluentAssertions;
using Lustrum.Core.Usage;

namespace Lustrum.Core.Tests;

public class TranscriptParserTests
{
    private const string At = "2026-08-26T17:44:17.469Z";

    internal static string ModelCall(string skill, string cwd = "/work/app", string at = At) =>
        $$$"""{"type":"assistant","timestamp":"{{{at}}}","cwd":"{{{cwd}}}","sessionId":"s1","message":{"role":"assistant","content":[{"type":"text","text":"ok"},{"type":"tool_use","id":"t1","name":"Skill","input":{"skill":"{{{skill}}}","args":"x"}}]}}""";

    internal static string UserCommand(string command, string cwd = "/work/app", string at = At) =>
        $$$"""{"type":"user","timestamp":"{{{at}}}","cwd":"{{{cwd}}}","sessionId":"s1","message":{"role":"user","content":"<command-name>/{{{command}}}</command-name>\n<command-args></command-args>"}}""";

    [Fact]
    public void Skill_tool_call_is_a_model_invocation()
    {
        var invocation = TranscriptParser.Parse(ModelCall("unity:ui")).Should().ContainSingle().Subject;

        invocation.Name.Should().Be("unity:ui");
        invocation.Kind.Should().Be(InvocationKind.Model);
        invocation.ProjectPath.Should().Be("/work/app");
        invocation.At.Should().Be(DateTimeOffset.Parse(At));
    }

    [Fact]
    public void Slash_command_is_a_user_invocation_without_the_slash()
    {
        var invocation = TranscriptParser.Parse(UserCommand("code-review")).Should().ContainSingle().Subject;

        invocation.Name.Should().Be("code-review");
        invocation.Kind.Should().Be(InvocationKind.User);
    }

    [Fact]
    public void Command_inside_content_blocks_is_found()
    {
        var line = $$$"""{"type":"user","timestamp":"{{{At}}}","cwd":"/w","message":{"content":[{"type":"text","text":"<command-name>/deploy</command-name>"}]}}""";

        TranscriptParser.Parse(line).Select(i => i.Name).Should().Equal("deploy");
    }

    [Fact]
    public void Command_with_json_escaped_angle_brackets_is_found()
    {
        var line = $$$"""{"type":"user","timestamp":"{{{At}}}","cwd":"/w","message":{"content":"\u003Ccommand-name\u003E/deploy\u003C/command-name\u003E"}}""";

        TranscriptParser.MightContainInvocation(line).Should().BeTrue();
        TranscriptParser.Parse(line).Select(i => i.Name).Should().Equal("deploy");
    }

    [Fact]
    public void Several_tool_calls_in_one_message_are_all_counted()
    {
        var line = $$$"""{"type":"assistant","timestamp":"{{{At}}}","cwd":"/w","message":{"content":[{"type":"tool_use","name":"Skill","input":{"skill":"a"}},{"type":"tool_use","name":"Read","input":{}},{"type":"tool_use","name":"Skill","input":{"skill":"b"}}]}}""";

        TranscriptParser.Parse(line).Select(i => i.Name).Should().Equal("a", "b");
    }

    [Theory]
    [InlineData("""{"type":"assistant","message":{"content":[{"type":"text","text":"talking about Skill"}]}}""")]
    [InlineData("""{"type":"user","message":{"content":"plain"}}""")]
    [InlineData("""{ not json "Skill" """)]
    [InlineData("")]
    [InlineData("""{"type":"assistant","timestamp":"bad","message":{"content":[{"type":"tool_use","name":"Skill","input":{"skill":"a"}}]}}""")]
    public void Unrelated_or_malformed_lines_yield_nothing(string line) =>
        TranscriptParser.Parse(line).Should().BeEmpty();

    [Fact]
    public void Lines_without_markers_are_skipped_cheaply()
    {
        TranscriptParser.MightContainInvocation("""{"type":"assistant","message":{}}""").Should().BeFalse();
        TranscriptParser.MightContainInvocation(ModelCall("a")).Should().BeTrue();
        TranscriptParser.MightContainInvocation(UserCommand("a")).Should().BeTrue();
    }
}

using FluentAssertions;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class ProjectPathDecoderTests
{
    [Fact]
    public void Encode_replaces_every_non_alphanumeric_with_dash()
    {
        ProjectPathDecoder.Encode("/Users/me/dev/my_app").Should().Be("-Users-me-dev-my-app");
        ProjectPathDecoder.Encode("/Users/me/a b/.claude").Should().Be("-Users-me-a-b--claude");
    }

    [Fact]
    public void Decodes_plain_path()
    {
        using var home = new FakeClaudeHome();
        var project = home.Project("dev/tossivo");

        var decoded = ProjectPathDecoder.Decode(ProjectPathDecoder.Encode(project));

        decoded.Exists.Should().BeTrue();
        decoded.Path.Should().Be(project);
    }

    [Fact]
    public void Decodes_dash_underscore_dot_and_space_in_folder_names()
    {
        using var home = new FakeClaudeHome();
        foreach (var name in new[] { "my-app", "my_tool", "site.io", "side project" })
        {
            var project = home.Project("dev/" + name);

            var decoded = ProjectPathDecoder.Decode(ProjectPathDecoder.Encode(project));

            decoded.Exists.Should().BeTrue(because: name);
            decoded.Path.Should().Be(project);
        }
    }

    [Fact]
    public void Decodes_folder_name_ending_in_a_separator_character()
    {
        using var home = new FakeClaudeHome();
        var project = home.Project("dev/app.");

        var decoded = ProjectPathDecoder.Decode(ProjectPathDecoder.Encode(project));

        decoded.Exists.Should().BeTrue();
        decoded.Path.Should().Be(project);
    }

    [Fact]
    public void Ambiguous_names_prefer_the_longest_matching_folder_name()
    {
        using var home = new FakeClaudeHome();
        var flat = home.Project("dev/a-b");
        home.Project("dev/a/b");

        var decoded = ProjectPathDecoder.Decode(ProjectPathDecoder.Encode(flat));

        decoded.Path.Should().Be(flat);
    }

    [Fact]
    public void Missing_windows_drive_path_is_marked_missing()
    {
        var decoded = ProjectPathDecoder.Decode("Q--no-such-folder-anywhere");

        decoded.Exists.Should().BeFalse();
        decoded.Path.Should().Be("Q--no-such-folder-anywhere");
    }

    [Fact]
    public void Unresolvable_name_is_returned_as_is_and_marked_missing()
    {
        var decoded = ProjectPathDecoder.Decode("-definitely-not-a-real-place-xyz");

        decoded.Exists.Should().BeFalse();
        decoded.Path.Should().Be("-definitely-not-a-real-place-xyz");
    }
}

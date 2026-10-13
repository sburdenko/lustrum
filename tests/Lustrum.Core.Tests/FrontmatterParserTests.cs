using FluentAssertions;
using Lustrum.Core.Parsing;

namespace Lustrum.Core.Tests;

public class FrontmatterParserTests
{
    [Fact]
    public void Parses_name_description_and_body()
    {
        var result = FrontmatterParser.Parse("---\nname: demo\ndescription: Does things\n---\n# Title\nbody\n");

        result.State.Should().Be(FrontmatterState.Parsed);
        result.Fields["name"].Should().Be("demo");
        result.Fields["description"].Should().Be("Does things");
        result.Body.Should().Be("# Title\nbody\n");
    }

    [Fact]
    public void Normalizes_bom_crlf_and_tabs()
    {
        var text = "﻿---\r\nname: demo\r\nmetadata:\r\n\tauthor: me\r\n---\r\nbody";

        var result = FrontmatterParser.Parse(text);

        result.State.Should().Be(FrontmatterState.Parsed);
        result.Fields["name"].Should().Be("demo");
        result.Fields["metadata"].Should().Contain("author");
        result.Body.Should().Be("body");
    }

    [Fact]
    public void Text_without_opening_fence_is_missing()
    {
        var result = FrontmatterParser.Parse("# Just markdown\nno frontmatter\n");

        result.State.Should().Be(FrontmatterState.Missing);
        result.Fields.Should().BeEmpty();
        result.Body.Should().StartWith("# Just markdown");
    }

    [Fact]
    public void Unclosed_fence_is_invalid()
    {
        var result = FrontmatterParser.Parse("---\nname: demo\nno closing fence\n");

        result.State.Should().Be(FrontmatterState.Invalid);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Broken_yaml_is_invalid_and_does_not_throw()
    {
        var result = FrontmatterParser.Parse("---\nname: [unclosed\n  : : :\n---\nbody");

        result.State.Should().Be(FrontmatterState.Invalid);
        result.Error.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void Allowed_tools_accepts_yaml_list()
    {
        var result = FrontmatterParser.Parse("---\nname: a\ndescription: b\nallowed-tools:\n  - Read\n  - Bash\n---\n");

        result.AllowedTools.Should().Equal("Read", "Bash");
    }

    [Fact]
    public void Allowed_tools_accepts_comma_string()
    {
        var result = FrontmatterParser.Parse("---\nname: a\ndescription: b\nallowed-tools: Read, Bash(git:*), Grep\n---\n");

        result.AllowedTools.Should().Equal("Read", "Bash(git:*)", "Grep");
    }

    [Fact]
    public void Empty_frontmatter_block_is_parsed_with_no_fields()
    {
        var result = FrontmatterParser.Parse("---\n---\nbody");

        result.State.Should().Be(FrontmatterState.Parsed);
        result.Fields.Should().BeEmpty();
        result.Body.Should().Be("body");
    }
}

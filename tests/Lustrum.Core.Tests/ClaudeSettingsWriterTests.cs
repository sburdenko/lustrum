using System.Text.Json.Nodes;
using FluentAssertions;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class ClaudeSettingsWriterTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();

    public void Dispose() => _home.Dispose();

    private string SettingsPath => Path.Combine(_home.ClaudeDir, "settings.local.json");

    private JsonNode Written() => JsonNode.Parse(File.ReadAllText(SettingsPath))!;

    [Fact]
    public void Creates_the_file_when_it_does_not_exist()
    {
        ClaudeSettingsWriter.SetSkillVisibility(SettingsPath, "deploy", SkillVisibility.Off);

        Written()["skillOverrides"]!["deploy"]!.GetValue<string>().Should().Be("off");
    }

    [Fact]
    public void Keeps_every_other_key_and_entry()
    {
        _home.File(SettingsPath, """{ "model": "opus", "enabledPlugins": { "a@m": false }, "skillOverrides": { "notes": "name-only" } }""");

        ClaudeSettingsWriter.SetSkillVisibility(SettingsPath, "deploy", SkillVisibility.UserInvocableOnly);

        var json = Written();
        json["model"]!.GetValue<string>().Should().Be("opus");
        json["enabledPlugins"]!["a@m"]!.GetValue<bool>().Should().BeFalse();
        json["skillOverrides"]!["notes"]!.GetValue<string>().Should().Be("name-only");
        json["skillOverrides"]!["deploy"]!.GetValue<string>().Should().Be("user-invocable-only");
    }

    [Fact]
    public void Removing_the_last_entry_removes_the_empty_map()
    {
        _home.File(SettingsPath, """{ "model": "opus", "skillOverrides": { "deploy": "off" } }""");

        ClaudeSettingsWriter.SetSkillVisibility(SettingsPath, "deploy", null);

        Written().AsObject().Select(p => p.Key).Should().Equal("model");
    }

    [Theory]
    [InlineData("{ // comment\n \"model\": \"opus\" }")]
    [InlineData("{ \"model\": \"opus\", }")]
    [InlineData("[1, 2]")]
    [InlineData("{ \"skillOverrides\": [\"deploy\"] }")]
    public void Refuses_files_it_cannot_rewrite_faithfully_and_leaves_them_untouched(string content)
    {
        _home.File(SettingsPath, content);

        var write = () => ClaudeSettingsWriter.SetSkillVisibility(SettingsPath, "deploy", SkillVisibility.Off);

        write.Should().Throw<SettingsWriteException>().Which.Message.Should().Contain("left alone");
        File.ReadAllText(SettingsPath).Should().Be(content);
        Directory.GetFiles(_home.ClaudeDir, "*.tmp").Should().BeEmpty();
    }

    [Fact]
    public void An_explicit_on_is_written_as_on()
    {
        ClaudeSettingsWriter.SetSkillVisibility(SettingsPath, "deploy", SkillVisibility.On);

        Written()["skillOverrides"]!["deploy"]!.GetValue<string>().Should().Be("on");
    }

    [Fact]
    public void An_empty_file_counts_as_no_settings()
    {
        _home.File(SettingsPath, "  \n");

        ClaudeSettingsWriter.SetSkillVisibility(SettingsPath, "deploy", SkillVisibility.Off);

        Written()["skillOverrides"]!["deploy"]!.GetValue<string>().Should().Be("off");
    }

    [Fact]
    public void Other_text_keeps_its_characters_unescaped()
    {
        _home.File(SettingsPath, """{ "hooks": "lint && test", "note": "Привет <tag> it's +1" }""");

        ClaudeSettingsWriter.SetSkillVisibility(SettingsPath, "deploy", SkillVisibility.Off);

        var text = File.ReadAllText(SettingsPath);
        text.Should().Contain("lint && test").And.Contain("Привет <tag> it's +1");
    }

    [Fact]
    public void A_linked_settings_file_stays_a_link_and_the_real_file_changes()
    {
        var real = _home.File(Path.Combine(_home.Home, "dotfiles", "settings.json"), """{ "model": "opus" }""");
        File.CreateSymbolicLink(SettingsPath, real);

        ClaudeSettingsWriter.SetSkillVisibility(SettingsPath, "deploy", SkillVisibility.Off);

        new FileInfo(SettingsPath).LinkTarget.Should().NotBeNull();
        JsonNode.Parse(File.ReadAllText(real))!["skillOverrides"]!["deploy"]!.GetValue<string>().Should().Be("off");
    }

    [Fact]
    public void The_file_mode_is_kept()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        _home.File(SettingsPath, """{ "env": { "TOKEN": "secret" } }""");
        File.SetUnixFileMode(SettingsPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        ClaudeSettingsWriter.SetSkillVisibility(SettingsPath, "deploy", SkillVisibility.Off);

        File.GetUnixFileMode(SettingsPath).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite);
    }

    [Fact]
    public void Refuses_to_write_without_a_path()
    {
        var write = () => ClaudeSettingsWriter.SetSkillVisibility(string.Empty, "deploy", SkillVisibility.Off);

        write.Should().Throw<SettingsWriteException>();
    }
}

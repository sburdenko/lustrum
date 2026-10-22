using FluentAssertions;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Tests.Fixtures;

namespace Lustrum.Core.Tests;

public class ManagedPolicyTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();

    public void Dispose() => _home.Dispose();

    private sealed class FakePolicies(IReadOnlyList<PolicyText>? device = null, PolicyText? user = null) : IPolicyStore
    {
        public IReadOnlyList<PolicyText> DevicePolicies() => device ?? [];

        public PolicyText? UserPolicy() => user;
    }

    private void Remote(string json) => _home.File(_home.Paths.RemoteSettingsJson, json);

    private void ManagedFile(string json) => _home.File(Path.Combine(_home.ManagedDir, "managed-settings.json"), json);

    private static string Hide(string skill) => $$"""{ "skillOverrides": { "{{skill}}": "off" } }""";

    private async Task<ScanResult> Scan(IPolicyStore? policies = null) =>
        await SkillScanner.ScanAsync(_home.Paths, policies: policies ?? new FakePolicies());

    [Fact]
    public async Task The_server_managed_cache_wins_over_device_policy_and_files_without_merging()
    {
        Remote(Hide("remote"));
        ManagedFile(Hide("file"));

        var scan = await Scan(new FakePolicies([new PolicyText("profile", Hide("device"))]));

        scan.Settings.Resolve("remote", null).Value.Should().Be(SkillVisibility.Off);
        scan.Settings.Resolve("device", null).Value.Should().Be(SkillVisibility.On);
        scan.Settings.Resolve("file", null).Value.Should().Be(SkillVisibility.On);
    }

    [Fact]
    public async Task Device_policy_wins_over_files_and_a_later_profile_wins_over_an_earlier_one()
    {
        ManagedFile(Hide("file"));
        var device = new FakePolicies([
            new PolicyText("device profile", """{ "skillOverrides": { "a": "off", "b": "off" } }"""),
            new PolicyText("user profile", """{ "skillOverrides": { "b": "name-only" } }"""),
        ]);

        var scan = await Scan(device);

        scan.Settings.Resolve("a", null).Value.Should().Be(SkillVisibility.Off);
        scan.Settings.Resolve("b", null).Value.Should().Be(SkillVisibility.NameOnly);
        scan.Settings.Resolve("file", null).Value.Should().Be(SkillVisibility.On);
    }

    [Fact]
    public async Task Merge_in_the_highest_source_lets_every_admin_source_contribute()
    {
        Remote("""{ "managedSourcesBehavior": "merge", "skillOverrides": { "shared": "name-only" } }""");
        ManagedFile("""{ "skillOverrides": { "shared": "off", "file": "off" } }""");

        var scan = await Scan(new FakePolicies([new PolicyText("profile", Hide("device"))]));

        scan.Settings.Resolve("shared", null).Value.Should().Be(SkillVisibility.NameOnly);
        scan.Settings.Resolve("file", null).Value.Should().Be(SkillVisibility.Off);
        scan.Settings.Resolve("device", null).Value.Should().Be(SkillVisibility.Off);
    }

    [Fact]
    public async Task A_source_without_policy_keys_is_skipped()
    {
        Remote("""{ }""");
        ManagedFile(Hide("file"));

        var scan = await Scan();

        scan.Settings.Resolve("file", null).Value.Should().Be(SkillVisibility.Off);
    }

    [Fact]
    public async Task The_user_registry_applies_only_without_any_admin_source()
    {
        var user = new PolicyText("HKCU", Hide("user"));

        (await Scan(new FakePolicies(user: user))).Settings.Resolve("user", null).Value.Should().Be(SkillVisibility.Off);

        ManagedFile("""{ "skillOverrides": { } }""");
        (await Scan(new FakePolicies(user: user))).Settings.Resolve("user", null).Value.Should().Be(SkillVisibility.On);
    }

    [Fact]
    public async Task An_unreadable_device_policy_is_reported_and_the_next_source_applies()
    {
        ManagedFile(Hide("file"));

        var scan = await Scan(new FakePolicies([new PolicyText("profile", null, "plutil failed")]));

        scan.Issues.Should().ContainSingle(i => i.Path == "profile" && i.Message.Contains("plutil failed"));
        scan.Settings.Resolve("file", null).Value.Should().Be(SkillVisibility.Off);
    }

    [Fact]
    public async Task A_merge_flag_with_no_policy_anywhere_means_no_managed_settings()
    {
        Remote("""{ "managedSourcesBehavior": "merge" }""");

        var scan = await Scan();

        scan.Settings.Managed.Should().BeNull();
    }

    [Fact]
    public async Task A_merge_flag_in_a_lower_source_is_ignored()
    {
        Remote(Hide("remote"));
        ManagedFile("""{ "managedSourcesBehavior": "merge", "skillOverrides": { "file": "off" } }""");

        var scan = await Scan();

        scan.Settings.Resolve("remote", null).Value.Should().Be(SkillVisibility.Off);
        scan.Settings.Resolve("file", null).Value.Should().Be(SkillVisibility.On);
    }

    [Fact]
    public async Task Any_key_counts_as_policy_so_a_source_with_only_a_schema_still_wins()
    {
        Remote("""{ "$schema": "https://json.schemastore.org/claude-code-settings.json" }""");
        ManagedFile(Hide("file"));

        var scan = await Scan();

        scan.Settings.Resolve("file", null).Value.Should().Be(SkillVisibility.On);
    }

    [Fact]
    public async Task A_broken_server_cache_is_reported_and_still_keeps_the_user_registry_out()
    {
        Remote("{ broken");

        var scan = await Scan(new FakePolicies(user: new PolicyText("HKCU", Hide("user"))));

        scan.Issues.Should().ContainSingle(i => i.Path == _home.Paths.RemoteSettingsJson);
        scan.Settings.Resolve("user", null).Value.Should().Be(SkillVisibility.On);
    }

    [Fact]
    public async Task Device_policy_can_turn_a_plugin_off_everywhere()
    {
        var kit = _home.InstallPlugin("m", "kit", "1");
        _home.File(Path.Combine(kit, "skills", "k", "SKILL.md"), "---\nname: k\ndescription: d\n---\n");

        var scan = await Scan(new FakePolicies([new PolicyText("profile", """{ "enabledPlugins": { "kit@m": false } }""")]));

        scan.Sources.Single(s => s.Kind == SourceKind.Plugin).IsEnabled.Should().BeFalse();
    }
}

public class SystemPolicyStoreTests : IDisposable
{
    private readonly FakeClaudeHome _home = new();

    public void Dispose() => _home.Dispose();

    [Fact]
    public void A_configuration_profile_is_converted_to_json_with_plutil()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var plist = _home.File(Path.Combine(_home.Home, "com.anthropic.claudecode.plist"), """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
            <plist version="1.0">
            <dict>
              <key>skillOverrides</key>
              <dict><key>deploy</key><string>off</string></dict>
              <key>enabledPlugins</key>
              <dict><key>kit@m</key><false/></dict>
            </dict>
            </plist>
            """);

        var text = SystemPolicyStore.ReadProfile(plist);

        text.Error.Should().BeNull();
        System.Text.Json.JsonDocument.Parse(text.Json!).RootElement.GetProperty("skillOverrides").GetProperty("deploy").GetString().Should().Be("off");
    }

    [Fact]
    public void A_corrupt_profile_reports_what_plutil_said()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        var plist = _home.File(Path.Combine(_home.Home, "broken.plist"), "<plist><dict><key>x</key>");

        var text = SystemPolicyStore.ReadProfile(plist);

        text.Json.Should().BeNull();
        text.Error.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void A_missing_converter_is_reported_instead_of_thrown()
    {
        var text = SystemPolicyStore.ReadProfile("/nowhere.plist", "/no/such/plutil-" + Guid.NewGuid().ToString("N"));

        text.Error.Should().StartWith("plutil could not run");
    }
}

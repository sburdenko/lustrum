using System.Diagnostics;
using FluentAssertions;
using Lustrum.App.Services;
using Lustrum.Core.Models;
using Lustrum.Core.Settings;

namespace Lustrum.App.Tests;

public class ServicesTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lustrum-services-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Settings_service_saves_and_reloads()
    {
        var path = Path.Combine(_dir, "settings.json");
        var service = new SettingsService(path);
        var changed = service.Current with { EditorCommand = "code", Theme = ThemePreference.Dark };

        service.TrySave(changed, out var error).Should().BeTrue();

        error.Should().BeEmpty();
        service.Current.Should().Be(changed);
        new SettingsService(path).Current.EditorCommand.Should().Be("code");
        service.LogFolder.Should().Be(Path.Combine(_dir, "logs"));
    }

    [Fact]
    public void Settings_service_reports_a_failed_save()
    {
        Directory.CreateDirectory(_dir);
        var blocker = Path.Combine(_dir, "file");
        File.WriteAllText(blocker, "x");
        var service = new SettingsService(Path.Combine(blocker, "settings.json"));

        service.TrySave(LustrumSettings.Default, out var error).Should().BeFalse();

        error.Should().NotBeEmpty();
    }

    [Fact]
    public void Scan_log_writes_summary_and_warnings()
    {
        var source = new SkillSource("project:/gone", SourceKind.Project, "gone", "/gone", null, false);
        var result = new ScanResult([source], [], [], [new ScanIssue("/x/SKILL.md", "Line 2: bad")], DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(12));

        using (var log = new ScanLog(_dir, Stopwatch.StartNew()))
        {
            log.Record(result);
        }

        var text = File.ReadAllText(Directory.GetFiles(_dir, "lustrum-*.log").Single());
        text.Should().Contain("Window shows data").And.Contain("Scanned 0 skills").And.Contain("Line 2: bad").And.Contain("/gone");
    }

    [Fact]
    public void Scan_log_writes_messages_with_braces_literally()
    {
        using (var log = new ScanLog(_dir, Stopwatch.StartNew()))
        {
            log.Info("bad json: { nope }");
        }

        File.ReadAllText(Directory.GetFiles(_dir, "lustrum-*.log").Single()).Should().Contain("bad json: { nope }");
    }

    [Fact]
    public void Scan_log_writes_each_read_issue_once_across_rescans()
    {
        var result = new ScanResult([], [], [], [new ScanIssue("/x/SKILL.md", "Line 2: bad")], DateTimeOffset.UtcNow, TimeSpan.Zero);

        using (var log = new ScanLog(_dir, Stopwatch.StartNew()))
        {
            log.Record(result);
            log.Record(result);
        }

        var text = File.ReadAllText(Directory.GetFiles(_dir, "lustrum-*.log").Single());
        text.Split("Line 2: bad").Length.Should().Be(2);
    }
}

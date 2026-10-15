using FluentAssertions;
using Lustrum.Core.Watching;

namespace Lustrum.Core.Tests;

public class WatchingTests : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "lustrum-watch-" + Guid.NewGuid().ToString("N"));

    public WatchingTests() => Directory.CreateDirectory(Path.Combine(_dir, "skills", "a"));

    private IReadOnlyList<WatchTarget> Targets => [new WatchTarget(_dir, Recursive: true)];

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public async Task Debounced_trigger_coalesces_a_burst_into_one_call()
    {
        var calls = 0;
        using var trigger = new DebouncedTrigger(TimeSpan.FromMilliseconds(80), () => Interlocked.Increment(ref calls));

        for (var i = 0; i < 10; i++)
        {
            trigger.Signal();
        }

        await Task.Delay(400);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task Debounced_trigger_ignores_signals_after_dispose()
    {
        var calls = 0;
        var trigger = new DebouncedTrigger(TimeSpan.FromMilliseconds(20), () => Interlocked.Increment(ref calls));
        trigger.Signal();
        trigger.Dispose();

        var act = () => trigger.Signal();

        act.Should().NotThrow();
        await Task.Delay(150);
        calls.Should().Be(0);
    }

    [Fact]
    public async Task Polling_watcher_does_not_report_its_own_baseline()
    {
        var changed = 0;
        var options = new WatchOptions(TimeSpan.FromMilliseconds(20), TimeSpan.FromMilliseconds(40), MaxWatchers: 0);
        using var watcher = new SkillTreeWatcher(Targets, options);
        watcher.Changed += () => Interlocked.Increment(ref changed);

        await Task.Delay(300);

        changed.Should().Be(0);
    }

    [Fact]
    public void Watcher_can_be_disposed_while_polling()
    {
        var options = new WatchOptions(TimeSpan.FromMilliseconds(5), TimeSpan.FromMilliseconds(1), MaxWatchers: 0);
        var watcher = new SkillTreeWatcher(Targets, options);

        var act = () => watcher.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Name_filtered_target_only_sees_matching_entries()
    {
        var filtered = new[] { new WatchTarget(_dir, Recursive: false, Filter: "settings.json") };
        var before = TreeFingerprint.Compute(filtered);

        File.WriteAllText(Path.Combine(_dir, "history.jsonl"), "noise");
        TreeFingerprint.Compute(filtered).Should().Be(before);

        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{}");
        TreeFingerprint.Compute(filtered).Should().NotBe(before);
    }

    [Fact]
    public void Fingerprint_changes_when_a_file_is_added_or_edited()
    {
        var before = TreeFingerprint.Compute(Targets);
        var file = Path.Combine(_dir, "skills", "a", "SKILL.md");
        File.WriteAllText(file, "one");
        var added = TreeFingerprint.Compute(Targets);
        File.WriteAllText(file, "two, longer");
        var edited = TreeFingerprint.Compute(Targets);

        added.Should().NotBe(before);
        edited.Should().NotBe(added);
        TreeFingerprint.Compute(Targets).Should().Be(edited);
    }

    [Fact]
    public async Task Polling_mode_reports_changes()
    {
        var changed = new TaskCompletionSource();
        var options = new WatchOptions(TimeSpan.FromMilliseconds(50), TimeSpan.FromMilliseconds(100), MaxWatchers: 0);
        using var watcher = new SkillTreeWatcher(Targets, options);
        watcher.Changed += () => changed.TrySetResult();
        watcher.IsPolling.Should().BeTrue();

        await File.WriteAllTextAsync(Path.Combine(_dir, "skills", "a", "SKILL.md"), "new");

        (await Task.WhenAny(changed.Task, Task.Delay(Patience))).Should().Be(changed.Task);
    }

    [Fact]
    public async Task Native_mode_reports_changes()
    {
        var changed = new TaskCompletionSource();
        var options = WatchOptions.Default with { Debounce = TimeSpan.FromMilliseconds(50) };
        using var watcher = new SkillTreeWatcher(Targets, options);
        watcher.Changed += () => changed.TrySetResult();
        watcher.IsPolling.Should().BeFalse();
        await Task.Delay(300);

        await File.WriteAllTextAsync(Path.Combine(_dir, "skills", "a", "SKILL.md"), "new");

        (await Task.WhenAny(changed.Task, Task.Delay(Patience))).Should().Be(changed.Task);
    }

    [Fact]
    public void Non_recursive_target_ignores_nested_changes()
    {
        var shallow = new[] { new WatchTarget(_dir, Recursive: false) };
        var before = TreeFingerprint.Compute(shallow);

        File.WriteAllText(Path.Combine(_dir, "skills", "a", "SKILL.md"), "nested");

        TreeFingerprint.Compute(shallow).Should().Be(before);
    }

    [Fact]
    public void Missing_directories_are_ignored()
    {
        using var watcher = new SkillTreeWatcher([new WatchTarget(Path.Combine(_dir, "missing"), Recursive: true)], WatchOptions.Default);

        watcher.WatchedCount.Should().Be(0);
    }
}

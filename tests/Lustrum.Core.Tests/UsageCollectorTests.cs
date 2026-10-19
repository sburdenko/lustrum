using FluentAssertions;
using Lustrum.Core.Usage;

namespace Lustrum.Core.Tests;

public class UsageCollectorTests : IDisposable
{
    private readonly string _projects = Path.Combine(Path.GetTempPath(), "lustrum-usage-" + Guid.NewGuid().ToString("N"));

    public UsageCollectorTests() => Directory.CreateDirectory(_projects);

    public void Dispose() => Directory.Delete(_projects, recursive: true);

    private string Transcript(string folder, string name, params string[] lines)
    {
        var dir = Path.Combine(_projects, folder);
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, name + ".jsonl");
        File.WriteAllText(path, string.Join('\n', lines) + "\n");
        return path;
    }

    [Fact]
    public async Task Aggregates_count_last_use_and_projects_per_skill()
    {
        Transcript("-work-app", "s1",
            TranscriptParserTests.ModelCall("review", "/work/app", "2026-09-01T10:00:00Z"),
            TranscriptParserTests.UserCommand("review", "/work/app", "2026-09-03T10:00:00Z"),
            """{"type":"assistant","message":{}}""");
        Transcript("-work-site", "s2", TranscriptParserTests.ModelCall("review", "/work/site", "2026-09-02T10:00:00Z"));
        Transcript("-work-site/sub/subagents", "agent", TranscriptParserTests.ModelCall("deploy", "/work/site", "2026-09-04T10:00:00Z"));

        var report = await new UsageCollector().CollectAsync(_projects, CancellationToken.None);

        var review = report.ByName["review"];
        review.Count.Should().Be(3);
        review.LastUsed.Should().Be(DateTimeOffset.Parse("2026-09-03T10:00:00Z"));
        review.Projects.Should().BeEquivalentTo("/work/app", "/work/site");
        review.ByModel.Should().Be(2);
        review.ByUser.Should().Be(1);
        review.Times.Should().Equal(
            DateTimeOffset.Parse("2026-09-01T10:00:00Z"),
            DateTimeOffset.Parse("2026-09-02T10:00:00Z"),
            DateTimeOffset.Parse("2026-09-03T10:00:00Z"));
        review.PerDay(new DateOnly(2026, 9, 3), 5, TimeZoneInfo.Utc).Should().Equal(0, 0, 1, 1, 1);
        report.ByName["deploy"].Count.Should().Be(1);
        report.TranscriptCount.Should().Be(3);
    }

    [Fact]
    public void Per_day_counts_use_the_local_day_and_ignore_uses_outside_the_window()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("plus3", TimeSpan.FromHours(3), "plus3", "plus3");
        var usage = new Usage.SkillUsage("x", 3, DateTimeOffset.Parse("2026-09-03T22:30:00Z"), [], 3, 0,
        [
            DateTimeOffset.Parse("2026-08-01T10:00:00Z"),
            DateTimeOffset.Parse("2026-09-02T20:59:00Z"),
            DateTimeOffset.Parse("2026-09-03T22:30:00Z"),
        ]);

        usage.PerDay(new DateOnly(2026, 9, 4), 3, zone).Should().Equal(1, 0, 1);
        new Usage.SkillUsage("y", 0, DateTimeOffset.MinValue, [], 0, 0).PerDay(new DateOnly(2026, 9, 4), 3, zone).Should().Equal(0, 0, 0);
    }

    [Fact]
    public async Task Appended_lines_are_picked_up_on_the_next_collect()
    {
        var path = Transcript("-w", "s", TranscriptParserTests.ModelCall("a"));
        var collector = new UsageCollector();
        (await collector.CollectAsync(_projects, CancellationToken.None)).ByName["a"].Count.Should().Be(1);

        File.AppendAllText(path, TranscriptParserTests.ModelCall("a") + "\n" + TranscriptParserTests.ModelCall("b") + "\n");
        var report = await collector.CollectAsync(_projects, CancellationToken.None);

        report.ByName["a"].Count.Should().Be(2);
        report.ByName["b"].Count.Should().Be(1);
    }

    [Fact]
    public async Task Unchanged_files_are_not_read_again()
    {
        Transcript("-w", "s", TranscriptParserTests.ModelCall("a"));
        var collector = new UsageCollector();
        await collector.CollectAsync(_projects, CancellationToken.None);

        var second = await collector.CollectAsync(_projects, CancellationToken.None);

        second.BytesRead.Should().Be(0);
        second.ByName["a"].Count.Should().Be(1);
    }

    [Fact]
    public async Task A_partial_last_line_is_read_once_it_is_complete()
    {
        var path = Path.Combine(_projects, "-w", "s.jsonl");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var line = TranscriptParserTests.ModelCall("a");
        File.WriteAllText(path, line[..20]);
        var collector = new UsageCollector();
        (await collector.CollectAsync(_projects, CancellationToken.None)).ByName.Should().BeEmpty();

        File.AppendAllText(path, line[20..] + "\n");

        (await collector.CollectAsync(_projects, CancellationToken.None)).ByName["a"].Count.Should().Be(1);
    }

    [Fact]
    public async Task Shrunk_or_deleted_files_are_recounted()
    {
        var path = Transcript("-w", "s", TranscriptParserTests.ModelCall("a"), TranscriptParserTests.ModelCall("a"));
        var collector = new UsageCollector();
        await collector.CollectAsync(_projects, CancellationToken.None);

        File.WriteAllText(path, TranscriptParserTests.ModelCall("a") + "\n");
        (await collector.CollectAsync(_projects, CancellationToken.None)).ByName["a"].Count.Should().Be(1);

        File.Delete(path);
        (await collector.CollectAsync(_projects, CancellationToken.None)).ByName.Should().BeEmpty();
    }

    [Fact]
    public async Task Escaped_command_markers_are_read_from_files()
    {
        Transcript("-w", "s", """{"type":"user","timestamp":"2026-09-01T10:00:00Z","cwd":"/w","message":{"content":"\u003Ccommand-name\u003E/deploy\u003C/command-name\u003E"}}""");

        var report = await new UsageCollector().CollectAsync(_projects, CancellationToken.None);

        report.For("deploy")!.ByUser.Should().Be(1);
    }

    [Fact]
    public async Task A_file_rewritten_with_the_same_length_is_read_again()
    {
        var path = Transcript("-w", "s", TranscriptParserTests.ModelCall("aaaa"));
        var collector = new UsageCollector();
        (await collector.CollectAsync(_projects, CancellationToken.None)).ByName.Keys.Should().Equal("aaaa");

        File.WriteAllText(path, TranscriptParserTests.ModelCall("bbbb") + "\n");
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddMinutes(1));

        (await collector.CollectAsync(_projects, CancellationToken.None)).ByName.Keys.Should().Equal("bbbb");
    }

    [Fact]
    public async Task A_file_replaced_by_a_longer_one_is_read_from_the_start()
    {
        var path = Transcript("-w", "s", TranscriptParserTests.ModelCall("aaaa"));
        var collector = new UsageCollector();
        await collector.CollectAsync(_projects, CancellationToken.None);

        File.WriteAllText(path, TranscriptParserTests.ModelCall("cccc", "/elsewhere/longer/path") + "\n" + TranscriptParserTests.ModelCall("dddd") + "\n");

        (await collector.CollectAsync(_projects, CancellationToken.None)).ByName.Keys.Should().BeEquivalentTo("cccc", "dddd");
    }

    [Fact]
    public async Task Oversized_lines_are_skipped_without_losing_the_lines_after_them()
    {
        var huge = "{\"type\":\"user\",\"blob\":\"" + new string('x', UsageCollector.MaxLineBytes + 10) + "\"}";
        Transcript("-w", "s", huge, TranscriptParserTests.ModelCall("after"));

        var report = await new UsageCollector().CollectAsync(_projects, CancellationToken.None);

        report.ByName.Keys.Should().Equal("after");
    }

    [Fact]
    public async Task Cancellation_stops_the_read()
    {
        Transcript("-w", "s", TranscriptParserTests.ModelCall("a"));
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        var act = () => new UsageCollector().CollectAsync(_projects, cancelled.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Missing_projects_folder_gives_an_empty_report()
    {
        var report = await new UsageCollector().CollectAsync(Path.Combine(_projects, "none"), CancellationToken.None);

        report.ByName.Should().BeEmpty();
        report.TranscriptCount.Should().Be(0);
    }

    [Fact]
    public async Task Usage_is_looked_up_by_full_name()
    {
        Transcript("-w", "s", TranscriptParserTests.ModelCall("kit:ui"));

        var report = await new UsageCollector().CollectAsync(_projects, CancellationToken.None);

        report.For("kit:ui")!.Count.Should().Be(1);
        report.For("ui").Should().BeNull();
    }
}

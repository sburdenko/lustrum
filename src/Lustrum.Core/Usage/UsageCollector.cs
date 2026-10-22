using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.Core.Usage;

/// <summary>
/// Collects skill usage from every transcript under ~/.claude/projects. Transcripts are append-only and can
/// add up to gigabytes, so the collector remembers how far it read each file and only reads what was added.
/// A hash of the start of each file detects files that were replaced rather than appended to. Lines longer
/// than <see cref="MaxLineBytes"/> (pasted images, huge tool output) are skipped. Calls are serialized; keep
/// one instance for the lifetime of the app.
/// </summary>
public sealed class UsageCollector
{
    public const int MaxLineBytes = 16 * 1024 * 1024;
    private const int BufferSize = 1 << 16;
    private const int HeadBytes = 4096;
    private static readonly byte[] SkillMarker = Encoding.UTF8.GetBytes(TranscriptParser.SkillMarker);
    private static readonly byte[] CommandMarker = Encoding.UTF8.GetBytes(TranscriptParser.CommandMarker);

    private readonly ConcurrentDictionary<string, FileState> _files = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<UsageReport> CollectAsync(string projectsDir, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            return await CollectLockedAsync(projectsDir, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<UsageReport> CollectLockedAsync(string projectsDir, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var issues = new ConcurrentBag<ScanIssue>();
        var transcripts = Transcripts(projectsDir, issues);
        long bytesRead = 0;

        var options = new ParallelOptions { CancellationToken = cancellationToken, MaxDegreeOfParallelism = Environment.ProcessorCount };
        await Parallel.ForEachAsync(transcripts, options, (path, token) =>
        {
            Interlocked.Add(ref bytesRead, Update(path, issues, token));
            return ValueTask.CompletedTask;
        });

        var present = transcripts.ToHashSet(StringComparer.Ordinal);
        foreach (var gone in _files.Keys.Where(k => !present.Contains(k)).ToList())
        {
            _files.TryRemove(gone, out _);
        }

        stopwatch.Stop();
        var sortedIssues = issues.OrderBy(i => i.Path, StringComparer.Ordinal).ToList();
        var invocations = _files.Values.SelectMany(f => f.Invocations).ToList();
        return new UsageReport(Aggregate(invocations), transcripts.Count, bytesRead, sortedIssues, stopwatch.Elapsed) { Invocations = invocations };
    }

    private static IReadOnlyList<string> Transcripts(string projectsDir, ConcurrentBag<ScanIssue> issues)
    {
        if (!Directory.Exists(projectsDir))
        {
            return [];
        }

        try
        {
            var options = new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true };
            return Directory.EnumerateFiles(projectsDir, "*.jsonl", options).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new ScanIssue(projectsDir, $"Cannot list transcripts: {ex.Message}"));
            return [];
        }
    }

    private long Update(string path, ConcurrentBag<ScanIssue> issues, CancellationToken cancellationToken)
    {
        try
        {
            var info = new FileInfo(path);
            var known = _files.GetValueOrDefault(path);
            if (known is not null && known.Length == info.Length && known.Modified == info.LastWriteTimeUtc)
            {
                return 0;
            }

            var resume = known is not null && info.Length >= known.Length && known.Head.SequenceEqual(HeadHash(path, known.HeadLength));
            var start = resume ? known!.Offset : 0;
            var found = new List<SkillInvocation>(resume ? known!.Invocations : []);
            var (offset, read) = ReadCompleteLines(path, start, found, cancellationToken);
            var headLength = (int)Math.Min(HeadBytes, offset);
            _files[path] = new FileState(info.Length, info.LastWriteTimeUtc, offset, headLength, HeadHash(path, headLength), found);
            return read;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            issues.Add(new ScanIssue(path, $"Cannot read transcript: {ex.Message}"));
            return 0;
        }
    }

    private static byte[] HeadHash(string path, int length)
    {
        using var stream = SharedFile.OpenRead(path);
        var head = new byte[length];
        var read = stream.ReadAtLeast(head, length, throwOnEndOfStream: false);
        return SHA256.HashData(head.AsSpan(0, read));
    }

    private static (long Offset, long Read) ReadCompleteLines(string path, long start, List<SkillInvocation> found, CancellationToken cancellationToken)
    {
        using var stream = SharedFile.OpenRead(path, BufferSize);
        stream.Seek(start, SeekOrigin.Begin);
        var buffer = new byte[BufferSize];
        var pending = new ArrayBufferWriter<byte>();
        var skipping = false;
        var offset = start;
        var position = start;
        long read = 0;
        int count;
        while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            read += count;
            var span = buffer.AsSpan(0, count);
            int newline;
            while ((newline = span.IndexOf((byte)'\n')) >= 0)
            {
                if (!skipping)
                {
                    if (pending.WrittenCount > 0)
                    {
                        pending.Write(span[..newline]);
                        Collect(pending.WrittenSpan, found);
                    }
                    else
                    {
                        Collect(span[..newline], found);
                    }
                }

                pending.ResetWrittenCount();
                skipping = false;
                position += newline + 1;
                offset = position;
                span = span[(newline + 1)..];
            }

            position += span.Length;
            if (skipping)
            {
                continue;
            }

            if (pending.WrittenCount + span.Length > MaxLineBytes)
            {
                skipping = true;
                pending.ResetWrittenCount();
            }
            else
            {
                pending.Write(span);
            }
        }

        return (offset, read);
    }

    private static void Collect(ReadOnlySpan<byte> line, List<SkillInvocation> found)
    {
        if (line.IndexOf(SkillMarker) < 0 && line.IndexOf(CommandMarker) < 0)
        {
            return;
        }

        found.AddRange(TranscriptParser.Parse(Encoding.UTF8.GetString(line)));
    }

    private static IReadOnlyDictionary<string, SkillUsage> Aggregate(IEnumerable<SkillInvocation> invocations) =>
        invocations
            .GroupBy(i => i.Name, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => Summarize(g.Key, g.ToList()), StringComparer.Ordinal);

    internal static SkillUsage Summarize(string name, IReadOnlyList<SkillInvocation> uses) =>
        new(
            name,
            uses.Count,
            uses.Max(i => i.At),
            uses.Select(i => i.ProjectPath).OfType<string>().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList(),
            uses.Count(i => i.Kind == InvocationKind.Model),
            uses.Count(i => i.Kind == InvocationKind.User),
            uses.Select(i => i.At).Order().ToList());

    private sealed record FileState(
        long Length,
        DateTime Modified,
        long Offset,
        int HeadLength,
        byte[] Head,
        IReadOnlyList<SkillInvocation> Invocations);
}

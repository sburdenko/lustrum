using System.Diagnostics;
using Lustrum.Core.Models;
using Serilog;
using Serilog.Core;

namespace Lustrum.App.Services;

public sealed class ScanLog : IDisposable
{
    private const int RetainedDays = 7;
    private const long MaxFileBytes = 10 * 1024 * 1024;
    private readonly Logger _logger;
    private readonly Stopwatch _sinceStart;
    private HashSet<string> _reported = [];
    private bool _firstScanLogged;

    public ScanLog(string folder, Stopwatch sinceStart)
    {
        _sinceStart = sinceStart;
        _logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(folder, "lustrum-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: RetainedDays,
                fileSizeLimitBytes: MaxFileBytes,
                rollOnFileSizeLimit: true)
            .CreateLogger();
    }

    public void Record(ScanResult result)
    {
        if (!_firstScanLogged)
        {
            _firstScanLogged = true;
            _logger.Information("Window shows data {Elapsed} ms after start", _sinceStart.ElapsedMilliseconds);
        }

        _logger.Information(
            "Scanned {Skills} skills from {Sources} sources in {Duration} ms",
            result.Skills.Count,
            result.Sources.Count,
            (int)result.Duration.TotalMilliseconds);

        var current = new HashSet<string>(StringComparer.Ordinal);
        foreach (var source in result.Sources.Where(s => !s.Exists))
        {
            Warn(current, $"source|{source.Id}", "Skipped {Source}: folder {Path} does not exist", source.Id, source.RootPath);
        }

        foreach (var issue in result.Issues)
        {
            Warn(current, $"issue|{issue.Path}|{issue.Message}", "Read issue at {Path}: {Message}", issue.Path, issue.Message);
        }

        _reported = current;
    }

    public void Info(string message) => _logger.Information("{Message}", message);

    public void Dispose() => _logger.Dispose();

    private void Warn(HashSet<string> current, string key, string template, string first, string second)
    {
        current.Add(key);
        if (!_reported.Contains(key))
        {
            _logger.Warning(template, first, second);
        }
    }
}

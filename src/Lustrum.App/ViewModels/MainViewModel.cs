using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lustrum.App.Services;
using Lustrum.Core.Indexing;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Settings;
using Lustrum.Core.Usage;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly Func<ScanOptions, CancellationToken, Task<ScanResult>> _scan;
    private readonly Func<CancellationToken, Task<UsageReport>> _usageSource;
    private readonly AppServices _services;
    private CancellationTokenSource? _scanCancellation;
    private ScanResult _result = Empty;
    private LustrumSettings _settings;
    private string? _persistentNotice;

    [ObservableProperty]
    private NavNode? _selectedNode;

    [ObservableProperty]
    private IReadOnlyList<NavNode> _navRoots = [];

    [ObservableProperty]
    private bool _showEmptySources;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _summary = "Scanning…";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _watchStatus = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public MainViewModel(
        Func<ScanOptions, CancellationToken, Task<ScanResult>> scan,
        Func<CancellationToken, Task<UsageReport>> usage,
        AppServices services)
    {
        _scan = scan;
        _usageSource = usage;
        _services = services;
        _settings = services.Settings.Current;
        _persistentNotice = services.Settings.Warning;
        ErrorMessage = _persistentNotice;
        services.Theme.Apply(_settings.Theme);
        SettingsEditor.Load(_settings);
        Chips = BuildChips([]);
    }

    public event Action<ScanResult>? ScanApplied;

    public ScanResult Result => _result;

    private static ScanResult Empty { get; } = new([], [], [], [], DateTimeOffset.MinValue, TimeSpan.Zero);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RescanAsync()
    {
        await ScanAsync();
        _ = RefreshUsageAsync();
    }

    public Task RescanFromWatcherAsync() => ScanAsync();

    private async Task ScanAsync()
    {
        _scanCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;
        IsScanning = true;
        ErrorMessage = _persistentNotice;
        try
        {
            var result = await _scan(_settings.ToScanOptions(), cancellation.Token);
            if (IsCurrent(cancellation))
            {
                Apply(result);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (IsCurrent(cancellation))
            {
                ErrorMessage = $"Scan failed: {ex.Message}";
            }
        }
        finally
        {
            if (IsCurrent(cancellation))
            {
                IsScanning = false;
                _scanCancellation = null;
            }

            cancellation.Dispose();
        }
    }

    private bool IsCurrent(CancellationTokenSource cancellation) =>
        ReferenceEquals(_scanCancellation, cancellation);

    partial void OnSelectedNodeChanged(NavNode? value)
    {
        Card = null;
        OnPropertyChanged(nameof(ListTitle));
        OnPropertyChanged(nameof(CanShowEffectiveSet));
        OnPropertyChanged(nameof(IsProblemsView));
        RefreshList();
    }

    partial void OnShowEmptySourcesChanged(bool value) => RebuildTree();

    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(IsEmptyResult));

    private void Apply(ScanResult result)
    {
        _result = result;
        Card = null;
        RebuildTree();
        Summary = BuildSummary(result);
        StatusText = BuildStatus(result);
        StartBodySearch();
        ScanApplied?.Invoke(result);
    }

    private void RebuildTree()
    {
        var scope = SelectedNode?.Scope;
        NavRoots = NavTreeBuilder.Build(_result, ShowEmptySources);
        var node = FindNode(NavRoots, scope) ?? NavRoots.FirstOrDefault();
        if (ReferenceEquals(node, SelectedNode))
        {
            RefreshList();
        }
        else
        {
            SelectedNode = node;
        }
    }

    private static NavNode? FindNode(IEnumerable<NavNode> nodes, SkillScope? scope)
    {
        if (scope is null)
        {
            return null;
        }

        foreach (var node in nodes)
        {
            if (node.Scope == scope)
            {
                return node;
            }

            var child = FindNode(node.Children, scope);
            if (child is not null)
            {
                return child;
            }
        }

        return null;
    }

    private static string BuildSummary(ScanResult result)
    {
        var existing = result.Sources.Count(s => s.Exists);
        return $"{Count(result.Skills.Count, "skill")} · "
            + $"{Count(existing, "source")} · "
            + $"{Count(result.Conflicts.Count, "conflict")}";
    }

    private static string BuildStatus(ScanResult result)
    {
        var issues = result.Issues.Count == 0 ? string.Empty : $" · {Count(result.Issues.Count, "read issue")}";
        return $"Scanned in {result.Duration.TotalSeconds:0.00} s · {result.ScannedAt.ToLocalTime():HH:mm:ss}{issues}";
    }

    private static string Count(int count, string noun) => count == 1 ? $"{count} {noun}" : $"{count} {noun}s";
}

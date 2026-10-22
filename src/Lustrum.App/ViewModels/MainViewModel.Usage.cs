using CommunityToolkit.Mvvm.ComponentModel;
using Lustrum.Core.Models;
using Lustrum.Core.Usage;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    private CancellationTokenSource? _usageRefresh;
    private string? _usageSignature;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Usage), nameof(UsageStatus))]
    private UsageState _usageState = UsageState.Loading;

    public UsageReport? Usage => UsageState.Report;

    public string UsageStatus => UsageState is LoadingUsage ? string.Empty : UsageState.Status;

    // A rescan after a SKILL.md edit rarely changes which copies share a name; only a change in those copies or the
    // sources redoes the split, which builds an effective set per project.
    private void ReattributeUsage(ScanResult result)
    {
        if (UsageState is not ReadyUsage ready)
        {
            return;
        }

        var signature = CopyUsage.Signature(result);
        if (signature != _usageSignature)
        {
            _usageSignature = signature;
            UsageState = ready with { Copies = CopyUsage.Attribute(result, ready.Value) };
        }
    }

    partial void OnUsageStateChanged(UsageState value)
    {
        foreach (var chip in Chips)
        {
            chip.IsAvailable = chip.Filter.IsAvailable(ChipContext);
        }

        Card?.ApplyUsage(value);
        RefreshList();
    }

    private async Task RefreshUsageAsync()
    {
        _usageRefresh?.Cancel();
        var cancellation = new CancellationTokenSource();
        _usageRefresh = cancellation;
        try
        {
            var report = await _usageSource(cancellation.Token);
            if (ReferenceEquals(_usageRefresh, cancellation))
            {
                _usageSignature = CopyUsage.Signature(_result);
                UsageState = new ReadyUsage(report, DateTimeOffset.Now, CopyUsage.Attribute(_result, report));
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            if (ReferenceEquals(_usageRefresh, cancellation))
            {
                UsageState = new FailedUsage(ex.Message);
                ErrorMessage = $"Could not read Claude Code transcripts: {ex.Message}";
            }
        }
        finally
        {
            if (ReferenceEquals(_usageRefresh, cancellation))
            {
                _usageRefresh = null;
            }

            cancellation.Dispose();
        }
    }
}

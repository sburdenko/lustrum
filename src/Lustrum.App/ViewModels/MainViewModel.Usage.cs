using CommunityToolkit.Mvvm.ComponentModel;
using Lustrum.Core.Usage;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    private CancellationTokenSource? _usageRefresh;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Usage), nameof(UsageStatus))]
    private UsageState _usageState = UsageState.Loading;

    public UsageReport? Usage => UsageState.Report;

    public string UsageStatus => UsageState is LoadingUsage ? string.Empty : UsageState.Status;

    partial void OnUsageStateChanged(UsageState value)
    {
        foreach (var chip in Chips)
        {
            chip.IsAvailable = !chip.Filter.NeedsUsage || value.CanJudgeUnused;
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
                UsageState = new ReadyUsage(report, DateTimeOffset.Now);
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

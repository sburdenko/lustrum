using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lustrum.App.Services;
using Lustrum.Core.Indexing;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private static readonly IReadOnlyList<SortOption> SortChoices =
    [
        new(SortMode.Name, "By name"),
        new(SortMode.Source, "By source"),
        new(SortMode.Modified, "By date"),
    ];

    private readonly Func<CancellationToken, Task<ScanResult>> _scan;
    private readonly IShellService _shell;
    private readonly IClipboardService _clipboard;
    private CancellationTokenSource? _scanCancellation;
    private bool _isRefreshingList;
    private ScanResult _result = Empty;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private NavNode? _selectedNode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(OpenFolderCommand), nameof(CopyPathCommand), nameof(CopyInvocationCommand))]
    private SkillItemViewModel? _selectedSkill;

    [ObservableProperty]
    private SkillCardViewModel? _card;

    [ObservableProperty]
    private IReadOnlyList<SkillItemViewModel> _visibleSkills = [];

    [ObservableProperty]
    private IReadOnlyList<NavNode> _navRoots = [];

    [ObservableProperty]
    private SortOption _selectedSort = SortChoices[0];

    [ObservableProperty]
    private bool _showEmptySources;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _summary = "Scanning…";

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string? _scanError;

    public MainViewModel(Func<CancellationToken, Task<ScanResult>> scan, IShellService shell, IClipboardService clipboard)
    {
        _scan = scan;
        _shell = shell;
        _clipboard = clipboard;
    }

    public IReadOnlyList<SortOption> Sorts => SortChoices;

    public bool IsEmptyResult => !IsScanning && VisibleSkills.Count == 0;

    public bool HasCard => Card is not null;

    public string ListTitle => SelectedNode?.Title ?? "All";

    private static ScanResult Empty { get; } = new([], [], [], [], DateTimeOffset.MinValue, TimeSpan.Zero);

    [RelayCommand(AllowConcurrentExecutions = true)]
    private async Task RescanAsync()
    {
        _scanCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        _scanCancellation = cancellation;
        IsScanning = true;
        ScanError = null;
        try
        {
            var result = await _scan(cancellation.Token);
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
                ScanError = $"Scan failed: {ex.Message}";
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

    [RelayCommand(CanExecute = nameof(HasSelectedSkill))]
    private void OpenFolder()
    {
        if (SelectedSkill is { } item && !_shell.RevealFile(item.Skill.Path))
        {
            ScanError = "Could not open the system file manager";
        }
    }

    [RelayCommand(CanExecute = nameof(HasSelectedSkill))]
    private Task CopyPathAsync() =>
        SelectedSkill is { } item ? _clipboard.SetTextAsync(item.Skill.Path) : Task.CompletedTask;

    [RelayCommand(CanExecute = nameof(HasSelectedSkill))]
    private Task CopyInvocationAsync() =>
        SelectedSkill is { } item ? _clipboard.SetTextAsync(item.Skill.InvocationName) : Task.CompletedTask;

    [RelayCommand]
    private void ClearSearch() => SearchText = string.Empty;

    private bool HasSelectedSkill() => SelectedSkill is not null;

    partial void OnSearchTextChanged(string value) => RefreshList();

    partial void OnSelectedNodeChanged(NavNode? value)
    {
        OnPropertyChanged(nameof(ListTitle));
        RefreshList();
    }

    partial void OnShowEmptySourcesChanged(bool value)
    {
        var scope = SelectedNode?.Scope;
        NavRoots = NavTreeBuilder.Build(_result, value);
        SelectedNode = FindNode(NavRoots, scope) ?? NavRoots.FirstOrDefault();
    }

    partial void OnSelectedSortChanged(SortOption value) => RefreshList();

    partial void OnIsScanningChanged(bool value) => OnPropertyChanged(nameof(IsEmptyResult));

    partial void OnVisibleSkillsChanged(IReadOnlyList<SkillItemViewModel> value) =>
        OnPropertyChanged(nameof(IsEmptyResult));

    partial void OnCardChanged(SkillCardViewModel? value) => OnPropertyChanged(nameof(HasCard));

    partial void OnSelectedSkillChanged(SkillItemViewModel? value)
    {
        if (!_isRefreshingList)
        {
            ShowCard(value);
        }
    }

    private void Apply(ScanResult result)
    {
        _result = result;
        var previousScope = SelectedNode?.Scope;
        Card = null;

        NavRoots = NavTreeBuilder.Build(result, ShowEmptySources);
        SelectedNode = FindNode(NavRoots, previousScope) ?? NavRoots.FirstOrDefault();
        RefreshList();

        Summary = BuildSummary(result);
        StatusText = BuildStatus(result);
    }

    private void RefreshList()
    {
        var keepId = SelectedSkill?.Skill.Id;
        var scope = SelectedNode?.Scope ?? SkillScope.All;
        var found = SkillIndex.Search(_result.Skills, new SkillQuery(scope, SearchText));

        _isRefreshingList = true;
        try
        {
            VisibleSkills = Sort(found).Select(s => new SkillItemViewModel(s)).ToList();
            SelectedSkill = VisibleSkills.FirstOrDefault(s => s.Skill.Id == keepId);
        }
        finally
        {
            _isRefreshingList = false;
        }

        ShowCard(SelectedSkill);
    }

    private IEnumerable<Skill> Sort(IEnumerable<Skill> skills) => SelectedSort.Mode switch
    {
        SortMode.Source => skills
            .OrderBy(s => s.Source.Kind)
            .ThenBy(s => s.Source.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.FullName, StringComparer.OrdinalIgnoreCase),
        SortMode.Modified => skills.OrderByDescending(s => s.Modified),
        _ => skills.OrderBy(s => s.FullName, StringComparer.OrdinalIgnoreCase),
    };

    private void ShowCard(SkillItemViewModel? item)
    {
        if (item is null)
        {
            Card = null;
            return;
        }

        if (Card?.Skill.Id == item.Skill.Id)
        {
            return;
        }

        var others = _result.Conflicts
            .FirstOrDefault(c => c.Name == item.Skill.Name)?.Skills
            .Where(s => s.Id != item.Skill.Id)
            .ToList() ?? [];

        var card = new SkillCardViewModel(item.Skill, others);
        Card = card;
        _ = LoadBodyAsync(card);
    }

    private static async Task LoadBodyAsync(SkillCardViewModel card)
    {
        try
        {
            card.Body = await Task.Run(() => SkillBodyReader.Read(card.Skill));
        }
        catch (Exception ex)
        {
            card.Body = $"Could not read the file: {ex.Message}";
        }
        finally
        {
            card.IsBodyLoading = false;
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

using CommunityToolkit.Mvvm.ComponentModel;
using Lustrum.Core.Comparing;
using Lustrum.Core.Git;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public sealed partial class ComparisonViewModel : ObservableObject
{
    public const int MaxRows = 2000;
    private const int Context = 3;
    private readonly Skill _skill;
    private int _version;

    [ObservableProperty]
    private CompareTarget? _target;

    [ObservableProperty]
    private IReadOnlyList<DiffRowViewModel> _rows = [];

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasTargets))]
    private IReadOnlyList<CompareTarget> _targets;

    public ComparisonViewModel(Skill skill, IReadOnlyList<Skill> others)
    {
        _skill = skill;
        _targets = others.Select(o => new SkillCompareTarget(SkillItemViewModel.SourceLabelFor(o.Source), o)).ToList();
    }

    public bool HasTargets => Targets.Count > 0;

    public string Legend => Target?.Legend ?? string.Empty;

    /// <summary>Offers what the fetched upstream changes, first in the list; null takes the offer back.</summary>
    public void ShowIncoming(SkillIncoming? incoming, string? upstream)
    {
        var current = Targets.OfType<IncomingCompareTarget>().FirstOrDefault()?.Incoming;
        if (Equals(current, incoming))
        {
            return;
        }

        var copies = Targets.Where(t => t is not IncomingCompareTarget).ToList();

        var wasShowing = Target is IncomingCompareTarget;
        Targets = incoming is null ? copies : [new IncomingCompareTarget($"incoming from {upstream}", incoming), .. copies];
        if (wasShowing)
        {
            Target = Targets.FirstOrDefault(t => t is IncomingCompareTarget) ?? Targets.FirstOrDefault();
        }
    }

    public void Reload()
    {
        if (Target is { } target)
        {
            _ = LoadAsync(target, ++_version);
        }
    }

    partial void OnTargetChanged(CompareTarget? value)
    {
        OnPropertyChanged(nameof(Legend));
        var version = ++_version;
        if (value is not null)
        {
            _ = LoadAsync(value, version);
            return;
        }

        Rows = [];
        Summary = string.Empty;
        IsLoading = false;
    }

    private async Task LoadAsync(CompareTarget target, int version)
    {
        IsLoading = true;
        Rows = [];
        Summary = string.Empty;
        try
        {
            var diff = await Task.Run(() =>
            {
                var (left, right) = target.Read(_skill);
                return SkillComparer.Compare(left, right);
            });
            if (version != _version)
            {
                return;
            }

            Rows = Cap(DiffFolding.Fold(diff.Lines, Context).Select(DiffRowViewModel.From).ToList());
            Summary = target.Summarize(diff);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (version == _version)
            {
                Rows = [];
                Summary = $"Could not compare: {ex.Message}";
            }
        }
        finally
        {
            if (version == _version)
            {
                IsLoading = false;
            }
        }
    }

    private static IReadOnlyList<DiffRowViewModel> Cap(IReadOnlyList<DiffRowViewModel> rows) =>
        rows.Count <= MaxRows
            ? rows
            : [.. rows.Take(MaxRows), DiffRowViewModel.Folded($"⋯ {rows.Count - MaxRows} more lines not shown")];
}

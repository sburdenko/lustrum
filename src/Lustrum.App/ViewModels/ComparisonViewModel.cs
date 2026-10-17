using CommunityToolkit.Mvvm.ComponentModel;
using Lustrum.Core.Comparing;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;

namespace Lustrum.App.ViewModels;

public sealed record CompareTarget(string Label, Skill Skill);

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

    public ComparisonViewModel(Skill skill, IReadOnlyList<Skill> others)
    {
        _skill = skill;
        Targets = others.Select(o => new CompareTarget(SkillItemViewModel.SourceLabelFor(o.Source), o)).ToList();
    }

    public IReadOnlyList<CompareTarget> Targets { get; }

    public string Legend => Target is null ? string.Empty : $"− only in this copy   + only in {Target.Label}";

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
        }
    }

    private async Task LoadAsync(CompareTarget target, int version)
    {
        IsLoading = true;
        Rows = [];
        Summary = string.Empty;
        try
        {
            var diff = await Task.Run(() => SkillComparer.Compare(
                SkillBodyReader.ReadText(_skill.Path),
                SkillBodyReader.ReadText(target.Skill.Path)));
            if (version != _version)
            {
                return;
            }

            Rows = Cap(DiffFolding.Fold(diff.Lines, Context).Select(DiffRowViewModel.From).ToList());
            Summary = diff.IsIdentical
                ? $"Identical to the copy in {target.Label}"
                : $"{Lines(diff.Removed)} only in this copy, {Lines(diff.Added)} only in {target.Label}";
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

    private static string Lines(int count) => count == 1 ? "1 line" : $"{count} lines";
}

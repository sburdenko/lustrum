using System.Collections.Immutable;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Lustrum.Core.Git;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    private const int ParallelChecks = 4;

    private UpdateIndex _updates = UpdateIndex.Empty;
    private CancellationTokenSource? _gitLoad;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckAllForUpdatesCommand))]
    private bool _isCheckingUpdates;

    [ObservableProperty]
    private string _updateStatus = string.Empty;

    public UpdateIndex Updates => _updates;

    private ChipContext ChipContext => new(UsageState, _updates);

    [RelayCommand(CanExecute = nameof(CanCheckAll))]
    private async Task CheckAllForUpdatesAsync()
    {
        IsCheckingUpdates = true;
        try
        {
            var skills = _result.Skills;
            var mapping = await Task.Run(() => MapRepositories(skills));
            _updates = _updates with { LocationBySkill = mapping };
            var repositories = mapping.Values.Select(l => l.Repository).DistinctBy(r => r.Root).ToList();
            UpdateStatus = repositories.Count == 0
                ? "No skills are in a git repository"
                : $"Checking {Count(repositories.Count, "repository", "repositories")} for updates…";
            using var gate = new SemaphoreSlim(ParallelChecks);
            await Task.WhenAll(repositories.Select(r => CheckRepositoryAsync(r, gate)));
            if (repositories.Count > 0)
            {
                UpdateStatus = CheckSummary(repositories);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            UpdateStatus = string.Empty;
            ErrorMessage = $"Could not check for updates: {ex.Message}";
        }
        finally
        {
            IsCheckingUpdates = false;
        }

        RefreshList();
    }

    [RelayCommand(CanExecute = nameof(CanCheckCard))]
    private async Task CheckCardForUpdatesAsync()
    {
        if (Card is not { GitLocation: { } location } card)
        {
            return;
        }

        _updates = _updates.Mapping(card.Skill, location);
        await CheckRepositoryAsync(location.Repository, null);
        RefreshList();
    }

    [RelayCommand(CanExecute = nameof(CanUpdateCard))]
    private async Task UpdateCardSkillAsync()
    {
        if (Card is not { GitRepository: { } repository })
        {
            return;
        }

        var before = _updates.StateOf(repository);
        SetRepoState(repository, new UpdatingRepo());
        try
        {
            var tracking = await _services.Git.UpdateAsync(repository, CancellationToken.None);
            SetRepoState(repository, new CheckedRepo(tracking, DateTimeOffset.Now));
            await ScanAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetRepoState(repository, before);
            ErrorMessage = $"Could not update {repository.Name}: {ex.Message}";
            if (Card is { } current && current.GitRepository == repository)
            {
                ReloadGit(current);
            }
        }

        RefreshList();
    }

    private bool CanCheckAll() => !IsCheckingUpdates && !_updates.IsUpdating;

    private bool CanCheckCard() => Card?.Git.CanCheck == true;

    private bool CanUpdateCard() => Card?.Git.CanUpdate == true;

    private async Task CheckRepositoryAsync(SkillRepository repository, SemaphoreSlim? gate)
    {
        if (_updates.StateOf(repository).IsBusy)
        {
            return;
        }

        var checking = new CheckingRepo();
        SetRepoState(repository, checking);
        RepoState outcome;
        if (gate is not null)
        {
            await gate.WaitAsync();
        }

        try
        {
            var tracking = await _services.Git.CheckForUpdatesAsync(repository, CancellationToken.None);
            outcome = new CheckedRepo(tracking, DateTimeOffset.Now, await CountIncomingAsync(repository, tracking));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            outcome = new FailedRepo(ex.Message);
        }
        finally
        {
            gate?.Release();
        }

        if (!ReferenceEquals(_updates.StateOf(repository), checking))
        {
            return;
        }

        SetRepoState(repository, outcome);
        if (Card is { } card && card.GitRepository == repository)
        {
            ReloadGit(card);
        }
    }

    // Counting is local and may fail on its own; the fetch still counts, with the repository-wide fallback.
    private async Task<IReadOnlyDictionary<string, int>?> CountIncomingAsync(SkillRepository repository, GitTracking tracking)
    {
        if (tracking.Behind == 0)
        {
            return null;
        }

        try
        {
            return await _services.Git.IncomingCommitsAsync(repository, _updates.PathsIn(repository), CancellationToken.None);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    // After a rescan, skills that were added or moved have no count yet; count them without fetching again.
    private async Task RecountMovedSkillsAsync()
    {
        foreach (var (root, state) in _updates.States)
        {
            if (state is not CheckedRepo done || _updates.LocationBySkill.Values.FirstOrDefault(l => l.Repository.Root == root)?.Repository is not { } repository
                || done.HasCountsFor(_updates.PathsIn(repository)))
            {
                continue;
            }

            var incoming = await CountIncomingAsync(repository, done.Tracking);
            if (ReferenceEquals(_updates.StateOf(repository), done))
            {
                SetRepoState(repository, done with { Incoming = incoming });
            }
        }
    }

    private void SetRepoState(SkillRepository repository, RepoState state)
    {
        _updates = _updates.With(repository, state);
        foreach (var chip in Chips)
        {
            chip.IsAvailable = chip.Filter.IsAvailable(ChipContext);
        }

        if (Card is { GitState: ReadyGit } card && card.GitRepository == repository)
        {
            card.ApplyGit(card.GitState, state);
        }

        RefreshGitCommands();
    }

    private void StartGitLoad(SkillCardViewModel card)
    {
        if (!GitRepositoryLocator.Applies(card.Skill))
        {
            _gitLoad?.Cancel();
            return;
        }

        card.ApplyGit(GitCardState.Loading, RepoState.NotChecked);
        ReloadGit(card);
    }

    private void ReloadGit(SkillCardViewModel card)
    {
        _gitLoad?.Cancel();
        var load = new CancellationTokenSource();
        _gitLoad = load;
        _ = LoadGitAsync(card, load);
    }

    private async Task LoadGitAsync(SkillCardViewModel card, CancellationTokenSource load)
    {
        GitCardState state;
        try
        {
            var report = await _services.Git.InspectAsync(card.Skill, load.Token);
            state = report is null ? GitCardState.NotInRepository : new ReadyGit(report);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            state = new FailedGit(ex is GitUnavailableException ? "git is not installed or could not be started" : ex.Message);
        }

        if (!ReferenceEquals(_gitLoad, load))
        {
            return;
        }

        _gitLoad = null;
        load.Dispose();
        var repo = state is ReadyGit ready ? _updates.StateOf(ready.Report.Repository) : RepoState.NotChecked;
        card.ApplyGit(state, repo);
        RefreshGitCommands();
    }

    private void RefreshGitCommands()
    {
        CheckCardForUpdatesCommand.NotifyCanExecuteChanged();
        UpdateCardSkillCommand.NotifyCanExecuteChanged();
        CheckAllForUpdatesCommand.NotifyCanExecuteChanged();
    }

    private void RemapRepositoriesIfChecked(ScanResult result)
    {
        if (!_updates.States.IsEmpty)
        {
            _ = RemapAsync(result);
        }
    }

    private async Task RemapAsync(ScanResult result)
    {
        try
        {
            var mapping = await Task.Run(() => MapRepositories(result.Skills));
            if (ReferenceEquals(_result, result))
            {
                _updates = _updates with { LocationBySkill = mapping };
                await RecountMovedSkillsAsync();
                RefreshList();
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Could not locate git repositories: {ex.Message}";
        }
    }

    private ImmutableDictionary<string, SkillLocation> MapRepositories(IEnumerable<Skill> skills) =>
        skills
            .Where(GitRepositoryLocator.Applies)
            .Select(s => (s.Id, Location: _services.Git.Locate(s)))
            .Where(p => p.Location is not null)
            .ToImmutableDictionary(p => p.Id, p => p.Location!);

    private string CheckSummary(IReadOnlyList<SkillRepository> repositories)
    {
        var failed = repositories.Count(r => _updates.StateOf(r) is FailedRepo);
        var behind = _result.Skills.Count(s => _updates.Incoming(s) > 0);
        var updates = behind == 0 ? "no skill has updates" : $"{Count(behind, "skill", "skills")} {(behind == 1 ? "has" : "have")} updates";
        var failures = failed == 0 ? string.Empty : $" · {failed} could not be checked";
        return $"Checked {Count(repositories.Count, "repository", "repositories")} at {DateTimeOffset.Now:HH:mm} · {updates}{failures}";
    }

    private static string Count(int count, string one, string many) => $"{count} {(count == 1 ? one : many)}";
}

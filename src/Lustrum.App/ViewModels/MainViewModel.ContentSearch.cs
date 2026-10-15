using CommunityToolkit.Mvvm.ComponentModel;
using Lustrum.Core.Indexing;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    private static readonly TimeSpan ContentSearchDelay = TimeSpan.FromMilliseconds(250);
    private static readonly IReadOnlySet<string> NoMatches = new HashSet<string>();

    private CancellationTokenSource? _contentSearch;
    private (string Text, IReadOnlySet<string> Ids) _contentMatches = (string.Empty, NoMatches);

    [ObservableProperty]
    private bool _searchInContent;

    partial void OnSearchInContentChanged(bool value)
    {
        RefreshList();
        StartBodySearch();
    }

    private IReadOnlyList<Skill> WithBodyMatches(IReadOnlyList<Skill> found, SkillScope scope, IReadOnlyList<string> tags)
    {
        if (!SearchInContent || _contentMatches.Text != SearchText || _contentMatches.Ids.Count == 0)
        {
            return found;
        }

        var already = found.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
        var extra = _result.Skills.Where(s =>
            _contentMatches.Ids.Contains(s.Id)
            && !already.Contains(s.Id)
            && scope.Includes(s)
            && tags.All(s.Tags.Contains));
        return found.Concat(extra).ToList();
    }

    private void StartBodySearch()
    {
        _contentSearch?.Cancel();
        _contentSearch = null;
        if (!SearchInContent || string.IsNullOrWhiteSpace(SearchText))
        {
            _contentMatches = (string.Empty, NoMatches);
            return;
        }

        var cancellation = new CancellationTokenSource();
        _contentSearch = cancellation;
        _ = RunBodySearchAsync(SearchText, cancellation.Token);
    }

    private async Task RunBodySearchAsync(string text, CancellationToken token)
    {
        try
        {
            await Task.Delay(ContentSearchDelay, token);
            var ids = await SkillBodySearch.FindAsync(_result.Skills, text, token);
            if (!token.IsCancellationRequested)
            {
                _contentMatches = (text, ids);
                RefreshList();
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Content search failed: {ex.Message}";
        }
    }
}

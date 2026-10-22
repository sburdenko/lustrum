using Lustrum.Core.Indexing;
using Lustrum.Core.Models;

namespace Lustrum.App.ViewModels;

public sealed partial class MainViewModel
{
    private static readonly HashSet<string> CliInputs =
    [
        nameof(SelectedNode), nameof(SelectedProjectView), nameof(SearchText), nameof(VisibleSkills), nameof(Card),
    ];

    private static readonly HashSet<string> FooterInputs =
    [
        nameof(VisibleSkills), nameof(VisibleProblems), nameof(StatusText), nameof(WatchStatus), nameof(UsageStatus), nameof(UpdateStatus),
    ];

    private static readonly HashSet<string> NoticeInputs = [nameof(ErrorMessage), nameof(ActionNotice)];

    protected override void OnPropertyChanged(System.ComponentModel.PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        var name = e.PropertyName ?? string.Empty;
        if (CliInputs.Contains(name))
        {
            OnPropertyChanged(nameof(CliCommand));
        }

        if (FooterInputs.Contains(name))
        {
            OnPropertyChanged(nameof(FooterStatus));
        }

        if (NoticeInputs.Contains(name))
        {
            OnPropertyChanged(nameof(HasErrorMessage));
            OnPropertyChanged(nameof(HasActionNotice));
        }
    }

    /// <summary>The lustrum CLI command that prints what the window shows, as far as the CLI can express it.</summary>
    public string CliCommand => CliFor(SelectedNode?.Scope, SelectedProject, ActiveView);

    public string FooterStatus => string.Join(
        " · ",
        new[] { RowCount, StatusText, WatchStatus, UsageStatus, UpdateStatus }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Length > 1 && char.IsUpper(p[0]) && char.IsLower(p[1]) ? char.ToLowerInvariant(p[0]) + p[1..] : p));

    private string RowCount => IsProblemsView
        ? (VisibleProblems.Count == 1 ? "1 problem" : $"{VisibleProblems.Count} problems")
        : (VisibleSkills.Count == 1 ? "1 row" : $"{VisibleSkills.Count} rows");

    public string SearchWatermark => _result.Skills.Count == 0 ? "Type to search" : $"Type to search {_result.Skills.Count} skills";

    public bool HasErrorMessage => !string.IsNullOrEmpty(ErrorMessage);

    public bool HasActionNotice => !string.IsNullOrEmpty(ActionNotice) && !HasErrorMessage;

    private string CliFor(SkillScope? scope, SkillSource? project, ProjectView view)
    {
        if (Card is { SelectedTab: SkillCardViewModel.CompareTab } card)
        {
            return $"diff {card.Skill.FullName}";
        }

        if (scope is ProblemsScope)
        {
            return WithSearch("problems");
        }

        var parts = new List<string> { "list" };
        if (project is not null)
        {
            parts.Add($"--project {Quote(Home(project.Id.StartsWith(ProjectPrefix, StringComparison.Ordinal) ? project.Id[ProjectPrefix.Length..] : project.RootPath))}");
            parts.Add(view switch
            {
                ProjectView.Missing => "--missing",
                ProjectView.Own => "--source project",
                _ when SelectedSort.Mode == SortMode.ContextCost => "--budget",
                _ => string.Empty,
            });
        }
        else
        {
            parts.Add(scope switch
            {
                KindScope kind => $"--source {kind.Kind.ToString().ToLowerInvariant()}",
                _ => string.Empty,
            });
        }

        var tags = Chips.Where(c => c.IsActive).Select(c => c.Label).ToList();
        if (tags.Contains("broken"))
        {
            parts.Add("--broken");
        }

        if (tags.Contains("unused"))
        {
            parts.Add("--unused");
        }

        var filter = scope is SourceScope or MarketplaceScope && project is null ? SelectedNode?.Title : null;
        var command = string.Join(' ', parts.Where(p => p.Length > 0));
        return WithSearch(filter is null ? command : $"{command} | grep -F {Quote(filter)}");
    }

    private string WithSearch(string command)
    {
        var text = SearchText.Trim();
        return text.Length == 0 ? command : $"{command} | grep -iF {Quote(text)}";
    }

    private const string ProjectPrefix = "project:";

    private static string Quote(string value) =>
        value.Length > 0 && value.All(c => char.IsLetterOrDigit(c) || "-_./~:@+".Contains(c))
            ? value
            : "'" + value.Replace("'", "'\\''") + "'";

    private static string Home(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return home.Length > 0 && path.StartsWith(home, StringComparison.Ordinal) ? "~" + path[home.Length..] : path;
    }
}

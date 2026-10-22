using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lustrum.App.Services;
using Lustrum.App.ViewModels;
using Lustrum.App.Views;
using System.Collections.Immutable;
using Lustrum.Core.ClaudeSettings;
using Lustrum.Core.Git;
using Lustrum.Core.Models;
using Lustrum.Core.Scanning;
using Lustrum.Core.Usage;
using Lustrum.Core.Watching;

namespace Lustrum.Screenshots;

internal sealed class Renderer(DemoHome demo)
{
    private const string ShownHome = "/home/dev";
    private static readonly TimeSpan Settle = TimeSpan.FromMilliseconds(1200);

    public async Task RenderAsync(Scenario scenario, string outputPath)
    {
        Application.Current!.RequestedThemeVariant = scenario.Theme;
        var paths = new ClaudePaths(demo.Home, demo.ManagedDir);
        var window = new MainWindow { Width = 1380, Height = 860 };
        var usage = new UsageCollector();
        var vm = new MainViewModel(
            async (options, token) => Hide(await SkillScanner.ScanAsync(paths, options, token), scenario.HidePaths),
            token => usage.CollectAsync(paths.ProjectsDir, token),
            new AppServices(new ShellService(), new AvaloniaClipboardService(window), new SettingsService(Path.Combine(demo.Home, "lustrum-settings.json")), new NoTheme(), new DemoGit(new SkillGit(new GitCli()), demo.Home, ShownHome), new SkillWriter()));
        window.DataContext = vm;
        window.Show();

        await vm.RescanCommand.ExecuteAsync(null);
        await WaitFor(() => vm.Usage is not null);
        vm.WatchStatus = await WatchStatusAsync(paths);
        if (scenario.CheckUpdates)
        {
            await vm.CheckAllForUpdatesCommand.ExecuteAsync(null);
        }

        Apply(scenario, vm, window);
        if (scenario.Tab == SkillCardViewModel.CompareTab)
        {
            await WaitFor(() => vm.Card!.HasComparison);
            vm.Card!.SelectedTab = scenario.Tab;
        }

        await Task.Delay(Settle);
        Dispatcher.UIThread.RunJobs();
        window.CaptureRenderedFrame()!.Save(outputPath);
        window.Close();
    }

    private static void Apply(Scenario scenario, MainViewModel vm, Window window)
    {
        if (scenario.Node is { } node)
        {
            vm.SelectedNode = Flatten(vm.NavRoots).First(n => n.Title == node);
        }

        vm.SelectedProjectView = vm.ProjectViews.Single(v => v.View == scenario.View);
        if (scenario.Chip is { } chip)
        {
            vm.Chips.Single(c => c.Label == chip).IsOn = true;
        }

        if (scenario.Problem is { } problem)
        {
            vm.SelectedProblem = vm.VisibleProblems.First(p => p.Subject == problem);
        }
        else
        {
            vm.SelectedSkill = scenario.Select is { } name
                ? vm.VisibleSkills.First(s => s.Skill.FullName == name && (scenario.SelectSource is null || s.Skill.Source.DisplayName == scenario.SelectSource))
                : vm.VisibleSkills.FirstOrDefault();
        }

        if (scenario.Tab > 0 && scenario.Tab != SkillCardViewModel.CompareTab)
        {
            vm.Card!.SelectedTab = scenario.Tab;
        }

        if (scenario.OpenCopyMenu)
        {
            var button = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "CopyToButton");
            button.Flyout!.ShowAt(button);
        }

        if (scenario.OpenSettings)
        {
            var button = window.GetVisualDescendants().OfType<Button>().First(b => b.Name == "SettingsButton");
            button.Flyout!.ShowAt(button);
        }
    }

    private static async Task<string> WatchStatusAsync(ClaudePaths paths)
    {
        var scan = await SkillScanner.ScanAsync(paths);
        using var watcher = new SkillTreeWatcher(WatchTargets.From(scan, paths), WatchOptions.Default);
        return $"Watching {watcher.WatchedCount} folders";
    }

    private ScanResult Hide(ScanResult scan, bool hidePaths)
    {
        if (!hidePaths)
        {
            return scan;
        }

        string P(string value) => value.Replace(demo.Home, ShownHome);
        var sources = scan.Sources.ToDictionary(s => s.Id, s => s with { Id = P(s.Id), RootPath = P(s.RootPath), DisplayName = P(s.DisplayName) });
        var skills = scan.Skills.Select(s => s with { Id = P(s.Id), Path = P(s.Path), Source = sources[s.Source.Id] }).ToList();
        var byId = skills.ToDictionary(s => s.Id);
        return scan with
        {
            Settings = Hide(scan.Settings, P),
            Sources = sources.Values.ToList(),
            Skills = skills,
            Conflicts = scan.Conflicts.Select(c => c with { Skills = c.Skills.Select(s => byId[P(s.Id)]).ToList() }).ToList(),
            Issues = scan.Issues.Select(i => i with { Path = P(i.Path) }).ToList(),
        };
    }

    private static SettingsLayers Hide(SettingsLayers overrides, Func<string, string> p) => overrides with
    {
        User = overrides.User with { Path = p(overrides.User.Path) },
        Projects = overrides.Projects.ToImmutableDictionary(
            e => p(e.Key),
            e => new ProjectSettings(e.Value.Shared with { Path = p(e.Value.Shared.Path) }, e.Value.Local with { Path = p(e.Value.Local.Path) })),
    };

    private static async Task WaitFor(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    private static IEnumerable<NavNode> Flatten(IEnumerable<NavNode> nodes) =>
        nodes.SelectMany(n => new[] { n }.Concat(Flatten(n.Children)));

    private sealed class NoTheme : IThemeService
    {
        public void Apply(Lustrum.Core.Settings.ThemePreference preference)
        {
        }
    }
}

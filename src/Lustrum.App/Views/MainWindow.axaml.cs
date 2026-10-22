using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Lustrum.App.ViewModels;

namespace Lustrum.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SettingsButton.Flyout!.Opened += (_, _) => ViewModel?.ResetSettingsEditorCommand.Execute(null);
        SearchBox.AddHandler(KeyDownEvent, OnKey, RoutingStrategies.Tunnel);
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (ViewModel is { } viewModel)
        {
            await viewModel.RescanCommand.ExecuteAsync(null);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        OnKey(this, e);
    }

    private void OnKey(object? sender, KeyEventArgs e)
    {
        if (e.Handled || ViewModel is not { } viewModel)
        {
            return;
        }

        var (shortcut, section) = KeyRouter.Route(e.Key, e.KeyModifiers, e.KeySymbol, ContextOf(e.Source));
        e.Handled = shortcut != Shortcut.None && Run(viewModel, shortcut, section);
    }

    private KeyContext ContextOf(object? source)
    {
        var visual = source as Visual;
        var inWindow = visual is not null && ReferenceEquals(visual.GetVisualRoot(), this);
        var inText = source is TextBox || visual?.FindAncestorOfType<ComboBox>(includeSelf: true) is not null;
        return new KeyContext(inWindow, inText, ReferenceEquals(source, SearchBox), List.IsListFocused);
    }

    private bool Run(MainViewModel viewModel, Shortcut shortcut, string? section)
    {
        switch (shortcut)
        {
            case Shortcut.FocusSearch:
                Dispatcher.UIThread.Post(FocusSearch);
                return true;
            case Shortcut.OpenSettings:
                SettingsButton.Flyout!.ShowAt(SettingsButton);
                return true;
            case Shortcut.FocusSources:
                Sources.FocusTree();
                return true;
            case Shortcut.FocusList:
                List.FocusList();
                return true;
            case Shortcut.FocusCard:
                CardPane.FocusCard();
                return true;
            case Shortcut.CopyPath:
                viewModel.CopyPathCommand.Execute(null);
                return true;
            case Shortcut.CopyName:
                viewModel.CopyInvocationCommand.Execute(null);
                return true;
            case Shortcut.Rescan:
                viewModel.RescanCommand.Execute(null);
                return true;
            case Shortcut.ClearSearch:
                viewModel.ClearSearchCommand.Execute(null);
                List.FocusList();
                return true;
            case Shortcut.Next:
                viewModel.MoveSelection(1);
                return true;
            case Shortcut.Previous:
                viewModel.MoveSelection(-1);
                return true;
            case Shortcut.OpenInEditor:
                viewModel.OpenInEditorCommand.Execute(null);
                return true;
            case Shortcut.ShowInFolder:
                viewModel.OpenFolderCommand.Execute(null);
                return true;
            case Shortcut.CheckUpdates:
                viewModel.CheckAllForUpdatesCommand.Execute(null);
                return true;
            case Shortcut.CopyTo when CardPane.CanCopyTo:
                CardPane.OpenCopyTo();
                return true;
            case Shortcut.Compare when viewModel.Card is { HasComparison: true } card:
                card.SelectedTab = SkillCardViewModel.CompareTab;
                return true;
            case Shortcut.Section when section is not null:
                return viewModel.SelectSection(section);
            default:
                return false;
        }
    }

    private void FocusSearch()
    {
        SearchBox.Focus();
        SearchBox.SelectAll();
    }
}

using Avalonia.Controls;
using Avalonia.Input;
using Lustrum.App.ViewModels;

namespace Lustrum.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        SettingsButton.Flyout!.Opened += (_, _) => ViewModel?.ResetSettingsEditorCommand.Execute(null);
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
        if (ViewModel is { } viewModel)
        {
            e.Handled = Handle(viewModel, e.Key, e.KeyModifiers);
        }
    }

    private bool Handle(MainViewModel viewModel, Key key, KeyModifiers modifiers)
    {
        var command = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        var shift = modifiers.HasFlag(KeyModifiers.Shift);
        switch (key)
        {
            case Key.F when command:
                SearchBox.Focus();
                SearchBox.SelectAll();
                return true;
            case Key.D1 when command:
                Sources.FocusTree();
                return true;
            case Key.D2 when command:
                List.FocusList();
                return true;
            case Key.D3 when command:
                CardPane.FocusCard();
                return true;
            case Key.C when command && shift:
                viewModel.CopyPathCommand.Execute(null);
                return true;
            case Key.C when command && List.IsListFocused:
                viewModel.CopyInvocationCommand.Execute(null);
                return true;
            case Key.F5:
                viewModel.RescanCommand.Execute(null);
                return true;
            case Key.Escape:
                viewModel.ClearSearchCommand.Execute(null);
                return true;
            default:
                return false;
        }
    }
}

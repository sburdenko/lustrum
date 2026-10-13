using Avalonia.Controls;
using Avalonia.Input;
using Lustrum.App.ViewModels;

namespace Lustrum.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.RescanCommand.ExecuteAsync(null);
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (DataContext is not MainViewModel viewModel)
        {
            return;
        }

        var shortcut = e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (shortcut && e.Key == Key.F)
        {
            SearchBox.Focus();
            SearchBox.SelectAll();
            e.Handled = true;
        }
        else if (e.Key == Key.F5)
        {
            viewModel.RescanCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            viewModel.ClearSearchCommand.Execute(null);
            e.Handled = true;
        }
    }
}

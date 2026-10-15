using Avalonia.Controls;

namespace Lustrum.App.Views;

public partial class SourcesPane : UserControl
{
    public SourcesPane()
    {
        InitializeComponent();
    }

    public void FocusTree() => Tree.Focus();
}

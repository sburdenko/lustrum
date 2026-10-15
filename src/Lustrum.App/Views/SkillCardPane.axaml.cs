using Avalonia.Controls;

namespace Lustrum.App.Views;

public partial class SkillCardPane : UserControl
{
    public SkillCardPane()
    {
        InitializeComponent();
    }

    public void FocusCard() => Tabs.Focus();
}

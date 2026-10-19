using Avalonia.Controls;

namespace Lustrum.App.Views;

public partial class SkillCardPane : UserControl
{
    public SkillCardPane()
    {
        InitializeComponent();
    }

    public bool CanCopyTo => CopyToButton.IsEffectivelyVisible;

    public void FocusCard() => Tabs.Focus();

    public void OpenCopyTo() => CopyToButton.Flyout?.ShowAt(CopyToButton);
}

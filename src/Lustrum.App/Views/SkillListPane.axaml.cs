using Avalonia.Controls;

namespace Lustrum.App.Views;

public partial class SkillListPane : UserControl
{
    public SkillListPane()
    {
        InitializeComponent();
    }

    public bool IsListFocused => SkillList.IsKeyboardFocusWithin;

    public void FocusList()
    {
        if (SkillList.SelectedIndex < 0 && SkillList.ItemCount > 0)
        {
            SkillList.SelectedIndex = 0;
        }

        SkillList.Focus();
    }
}

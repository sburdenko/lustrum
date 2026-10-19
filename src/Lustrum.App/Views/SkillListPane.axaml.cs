using Avalonia.Controls;
using Avalonia.Threading;
using Lustrum.App.ViewModels;

namespace Lustrum.App.Views;

public partial class SkillListPane : UserControl
{
    public SkillListPane()
    {
        InitializeComponent();
        SkillList.SelectionChanged += (_, _) => OnSkillSelected();
    }

    public bool IsListFocused => SkillList.IsKeyboardFocusWithin || ProblemList.IsKeyboardFocusWithin;

    public void FocusList()
    {
        var list = ProblemList.IsEffectivelyVisible ? ProblemList : SkillList;
        list.Focus();
    }

    // Headings are not selectable, but keyboard navigation can still land on one; step past it, and keep the
    // heading of the first row in a group in view.
    private void OnSkillSelected()
    {
        var rows = SkillList.ItemsSource as IReadOnlyList<IListRow>;
        if (rows is null || SkillList.SelectedItem is not IListRow selected)
        {
            return;
        }

        var index = IndexOf(rows, selected);
        if (!selected.IsSelectable)
        {
            var next = rows.Skip(index + 1).FirstOrDefault(r => r.IsSelectable);
            Dispatcher.UIThread.Post(() => SkillList.SelectedItem = next);
            return;
        }

        if (index > 0 && !rows[index - 1].IsSelectable)
        {
            SkillList.ScrollIntoView(index - 1);
        }

        SkillList.ScrollIntoView(selected);
    }

    private static int IndexOf(IReadOnlyList<IListRow> rows, IListRow row)
    {
        for (var i = 0; i < rows.Count; i++)
        {
            if (ReferenceEquals(rows[i], row))
            {
                return i;
            }
        }

        return -1;
    }
}

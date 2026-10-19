using Avalonia.Input;
using FluentAssertions;
using Lustrum.App.Views;

namespace Lustrum.App.Tests;

public class KeyRouterTests
{
    private static readonly KeyContext List = new(InMainWindow: true, InTextInput: false, InSearchBox: false, ListFocused: true);
    private static readonly KeyContext Search = new(InMainWindow: true, InTextInput: true, InSearchBox: true, ListFocused: false);
    private static readonly KeyContext Typing = new(InMainWindow: true, InTextInput: true, InSearchBox: false, ListFocused: false);
    private static readonly KeyContext Popup = new(InMainWindow: false, InTextInput: false, InSearchBox: false, ListFocused: false);

    private static Shortcut Route(Key key, KeyContext context, KeyModifiers modifiers = KeyModifiers.None, string? symbol = null) =>
        KeyRouter.Route(key, modifiers, symbol, context).Shortcut;

    [Theory]
    [InlineData(Key.J, Shortcut.Next)]
    [InlineData(Key.K, Shortcut.Previous)]
    [InlineData(Key.Enter, Shortcut.OpenInEditor)]
    [InlineData(Key.O, Shortcut.ShowInFolder)]
    [InlineData(Key.U, Shortcut.CheckUpdates)]
    [InlineData(Key.C, Shortcut.CopyTo)]
    [InlineData(Key.D, Shortcut.Compare)]
    [InlineData(Key.Escape, Shortcut.ClearSearch)]
    [InlineData(Key.Oem2, Shortcut.FocusSearch)]
    public void Plain_keys_act_on_the_list(Key key, Shortcut expected) => Route(key, List).Should().Be(expected);

    [Fact]
    public void Letters_jump_to_sections_and_shift_1_to_problems()
    {
        KeyRouter.Route(Key.G, KeyModifiers.None, "g", List).Should().Be((Shortcut.Section, "G"));
        KeyRouter.Route(Key.D1, KeyModifiers.Shift, "!", List).Should().Be((Shortcut.Section, "!"));
    }

    [Fact]
    public void Arrow_keys_are_left_to_a_focused_list_but_move_it_from_elsewhere()
    {
        Route(Key.Down, List).Should().Be(Shortcut.None);
        Route(Key.Down, List with { ListFocused = false }).Should().Be(Shortcut.Next);
    }

    [Theory]
    [InlineData(Key.U)]
    [InlineData(Key.O)]
    [InlineData(Key.Enter)]
    [InlineData(Key.G)]
    public void Shifted_letters_do_nothing(Key key) => Route(key, List, KeyModifiers.Shift).Should().Be(Shortcut.None);

    [Theory]
    [InlineData(Key.U)]
    [InlineData(Key.J)]
    [InlineData(Key.Escape)]
    [InlineData(Key.Enter)]
    public void Typing_in_settings_never_triggers_shortcuts(Key key) => Route(key, Typing).Should().Be(Shortcut.None);

    [Theory]
    [InlineData(Key.U)]
    [InlineData(Key.Escape)]
    [InlineData(Key.F5)]
    public void Keys_inside_a_popup_are_left_to_it(Key key) => Route(key, Popup).Should().Be(Shortcut.None);

    [Theory]
    [InlineData(Key.Down, Shortcut.Next)]
    [InlineData(Key.Up, Shortcut.Previous)]
    [InlineData(Key.Enter, Shortcut.FocusList)]
    [InlineData(Key.Escape, Shortcut.ClearSearch)]
    [InlineData(Key.J, Shortcut.None)]
    public void The_search_box_hands_navigation_to_the_list(Key key, Shortcut expected) => Route(key, Search).Should().Be(expected);

    [Fact]
    public void Command_shortcuts_work_everywhere_in_the_window_but_copy_name_leaves_text_boxes_alone()
    {
        Route(Key.F, Typing, KeyModifiers.Meta).Should().Be(Shortcut.FocusSearch);
        Route(Key.OemComma, List, KeyModifiers.Control).Should().Be(Shortcut.OpenSettings);
        Route(Key.F5, Typing).Should().Be(Shortcut.Rescan);
        Route(Key.C, List, KeyModifiers.Meta | KeyModifiers.Shift).Should().Be(Shortcut.CopyPath);
        Route(Key.C, List, KeyModifiers.Meta).Should().Be(Shortcut.CopyName);
        Route(Key.C, Search, KeyModifiers.Meta).Should().Be(Shortcut.None);
    }
}

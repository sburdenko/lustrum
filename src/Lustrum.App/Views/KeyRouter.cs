using Avalonia.Input;

namespace Lustrum.App.Views;

/// <summary>Where a key press came from, as far as shortcut routing cares.</summary>
public sealed record KeyContext(bool InMainWindow, bool InTextInput, bool InSearchBox, bool ListFocused);

/// <summary>
/// Maps a key press to a window shortcut. Plain-letter shortcuts only fire from the main window and never while text
/// is being typed or a dropdown has focus, so typing in Settings or picking a menu item cannot trigger them.
/// </summary>
public static class KeyRouter
{
    private static readonly Dictionary<Key, string> Sections = new()
    {
        [Key.A] = "A",
        [Key.G] = "G",
        [Key.P] = "P",
        [Key.L] = "L",
        [Key.S] = "S",
    };

    public static (Shortcut Shortcut, string? Section) Route(Key key, KeyModifiers modifiers, string? symbol, KeyContext context)
    {
        if (!context.InMainWindow)
        {
            return (Shortcut.None, null);
        }

        var command = modifiers.HasFlag(KeyModifiers.Control) || modifiers.HasFlag(KeyModifiers.Meta);
        if (command || key == Key.F5)
        {
            return (Command(key, modifiers.HasFlag(KeyModifiers.Shift), context), null);
        }

        if (context.InSearchBox)
        {
            return (Search(key), null);
        }

        if (context.InTextInput)
        {
            return (Shortcut.None, null);
        }

        return Plain(key, modifiers, symbol, context);
    }

    private static Shortcut Command(Key key, bool shift, KeyContext context) => key switch
    {
        Key.F5 => Shortcut.Rescan,
        Key.F => Shortcut.FocusSearch,
        Key.OemComma => Shortcut.OpenSettings,
        Key.D1 => Shortcut.FocusSources,
        Key.D2 => Shortcut.FocusList,
        Key.D3 => Shortcut.FocusCard,
        Key.C when shift => Shortcut.CopyPath,
        Key.C when !context.InTextInput => Shortcut.CopyName,
        _ => Shortcut.None,
    };

    private static Shortcut Search(Key key) => key switch
    {
        Key.Down => Shortcut.Next,
        Key.Up => Shortcut.Previous,
        Key.Enter => Shortcut.FocusList,
        Key.Escape => Shortcut.ClearSearch,
        _ => Shortcut.None,
    };

    private static (Shortcut, string?) Plain(Key key, KeyModifiers modifiers, string? symbol, KeyContext context)
    {
        if (modifiers == KeyModifiers.Shift && (key == Key.D1 || symbol == "!"))
        {
            return (Shortcut.Section, "!");
        }

        if (modifiers != KeyModifiers.None)
        {
            return (Shortcut.None, null);
        }

        if (symbol == "/" || key is Key.Oem2 or Key.Divide)
        {
            return (Shortcut.FocusSearch, null);
        }

        return key switch
        {
            Key.J => (Shortcut.Next, null),
            Key.K => (Shortcut.Previous, null),
            Key.Down when !context.ListFocused => (Shortcut.Next, null),
            Key.Up when !context.ListFocused => (Shortcut.Previous, null),
            Key.Enter => (Shortcut.OpenInEditor, null),
            Key.O => (Shortcut.ShowInFolder, null),
            Key.U => (Shortcut.CheckUpdates, null),
            Key.C => (Shortcut.CopyTo, null),
            Key.D => (Shortcut.Compare, null),
            Key.Escape => (Shortcut.ClearSearch, null),
            _ when Sections.TryGetValue(key, out var section) => (Shortcut.Section, section),
            _ => (Shortcut.None, null),
        };
    }
}

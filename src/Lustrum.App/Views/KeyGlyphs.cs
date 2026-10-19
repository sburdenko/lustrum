namespace Lustrum.App.Views;

/// <summary>Shortcut labels in the platform's own notation: ⌘ on macOS, Ctrl elsewhere.</summary>
public static class KeyGlyphs
{
    private static readonly bool Mac = OperatingSystem.IsMacOS();

    public static string Settings { get; } = Mac ? "⌘ ," : "Ctrl+,";

    public static string CopyName { get; } = Mac ? "⌘C" : "Ctrl+C";

    public static string CopyPath { get; } = Mac ? "⇧⌘C" : "Ctrl+Shift+C";
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace Lustrum.App.Controls;

/// <summary>
/// The census-grid mark: a 5×5 scan in which the found cells form an L, one of them in the accent colour.
/// Drawn on a 100-unit grid and scaled to the control size.
/// </summary>
public sealed class LustrumMark : Control
{
    public static readonly StyledProperty<MarkTone> ToneProperty =
        AvaloniaProperty.Register<LustrumMark, MarkTone>(nameof(Tone));

    private const double Grid = 100;
    private const double Dot = 10;
    private const double Origin = 23;
    private const double Step = 13;

    private static readonly Color Lime = Color.Parse("#C9F14C");
    private static readonly Color Ink = Color.Parse("#0E0E0C");
    private static readonly Color Paper = Color.Parse("#F3F3EF");

    static LustrumMark() => AffectsRender<LustrumMark>(ToneProperty);

    public MarkTone Tone
    {
        get => GetValue(ToneProperty);
        set => SetValue(ToneProperty, value);
    }

    public static bool IsLit(int column, int row) => column == 0 || row == 4 && column <= 3;

    public static bool IsAccent(int column, int row) => column == 0 && row == 2;

    public override void Render(DrawingContext context)
    {
        var size = Math.Min(Bounds.Width, Bounds.Height);
        if (size <= 0)
        {
            return;
        }

        var (tile, glyph, accent, dim) = Palette(Tone);
        var scale = size / Grid;
        context.DrawRectangle(new SolidColorBrush(tile), null, new Rect(0, 0, size, size), size * 0.22, size * 0.22);
        for (var row = 0; row < 5; row++)
        {
            for (var column = 0; column < 5; column++)
            {
                var color = IsAccent(column, row) ? accent : IsLit(column, row) ? glyph : dim;
                var center = new Point((Origin + column * Step + Dot / 2) * scale, (Origin + row * Step + Dot / 2) * scale);
                context.DrawEllipse(new SolidColorBrush(color), null, center, Dot / 2 * scale, Dot / 2 * scale);
            }
        }
    }

    private static (Color Tile, Color Glyph, Color Accent, Color Dim) Palette(MarkTone tone) => tone switch
    {
        MarkTone.Lime => (Lime, Ink, Paper, Color.FromArgb(41, Ink.R, Ink.G, Ink.B)),
        MarkTone.Paper => (Paper, Ink, Color.Parse("#769400"), Color.FromArgb(36, Ink.R, Ink.G, Ink.B)),
        _ => (Ink, Paper, Lime, Color.FromArgb(46, Paper.R, Paper.G, Paper.B)),
    };
}

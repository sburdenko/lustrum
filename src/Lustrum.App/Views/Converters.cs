using Avalonia;
using Avalonia.Data.Converters;

namespace Lustrum.App.Views;

public static class Converters
{
    public static FuncValueConverter<double, Thickness> LeftMargin { get; } = new(left => new Thickness(left, 5, 18, 5));

    public static FuncValueConverter<bool, Thickness> RightRule { get; } = new(last => last ? new Thickness(0) : new Thickness(0, 0, 1, 0));
}

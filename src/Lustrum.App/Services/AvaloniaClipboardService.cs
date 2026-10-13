using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace Lustrum.App.Services;

public sealed class AvaloniaClipboardService(TopLevel owner) : IClipboardService
{
    public async Task SetTextAsync(string text)
    {
        var clipboard = owner.Clipboard;
        if (clipboard is not null)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}

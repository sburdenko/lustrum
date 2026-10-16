using Avalonia;
using Avalonia.Headless;
using Avalonia.Threading;
using Lustrum.Screenshots;

var output = args.Length > 0 ? args[0] : "docs";
Directory.CreateDirectory(output);

AppBuilder.Configure<Lustrum.App.App>()
    .UseSkia()
    .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .SetupWithoutStarting();

using var demo = new DemoHome();
var renderer = new Renderer(demo);
var done = new CancellationTokenSource();
Dispatcher.UIThread.Post(async () =>
{
    try
    {
        foreach (var scenario in Scenario.Readme)
        {
            var path = Path.Combine(output, scenario.File);
            await renderer.RenderAsync(scenario, path);
            Console.WriteLine($"wrote {path}");
        }
    }
    finally
    {
        done.Cancel();
    }
});
Dispatcher.UIThread.MainLoop(done.Token);

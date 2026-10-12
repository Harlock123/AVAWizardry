using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(Wizardry.Desktop.Tests.TestAppBuilder))]

namespace Wizardry.Desktop.Tests;

public class TestAppBuilder
{
    // Skia rendering (instead of headless drawing) so frames can be captured for screenshots.
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .WithInterFont()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}

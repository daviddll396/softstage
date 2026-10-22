using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Softstage.Mac;
using Xunit;

// Avalonia.Headless.XUnit starts the app with this builder, so [AvaloniaFact] tests run without a display.
[assembly: AvaloniaTestApplication(typeof(Softstage.Mac.Tests.TestAppBuilder))]

// Tests change static overrides (AppPaths, FinderQuickAction), so they must not run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]

namespace Softstage.Mac.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .UseSkia();
}

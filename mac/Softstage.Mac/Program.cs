using Avalonia;
using Softstage.Core;
using Softstage.Mac.Services;

namespace Softstage.Mac;

internal static class Program
{
    private const string Usage =
        "Usage: Softstage\n" +
        "       Softstage --generate <image> [<image> ...]";

    [STAThread]
    public static int Main(string[] args)
    {
        // The CLI decides before any window is created, so Finder can run the app without a UI.
        if (args.Length == 0)
            return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

        if (args[0] == "--generate")
            return Generate(args.Skip(1).ToArray());

        Console.Error.WriteLine(Usage);
        return 2;
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    private static int Generate(IReadOnlyList<string> paths)
    {
        if (paths.Count is < 1 or > Compositor.MaximumImages)
        {
            Console.Error.WriteLine($"Choose between one and {Compositor.MaximumImages} images.");
            return 1;
        }

        try
        {
            var (settings, warning) = SettingsStore.Load(AppPaths.SettingsFile);
            if (warning is not null)
                Console.Error.WriteLine(warning);

            var result = Compositor.Generate(paths, settings);
            Console.Out.WriteLine(result.OutputPath);
            return 0;
        }
        catch (SoftstageException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Softstage could not create the composition. {ex.Message}");
            return 1;
        }
    }
}

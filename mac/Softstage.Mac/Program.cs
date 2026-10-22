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

        return Run(args, Console.Out, Console.Error);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();

    /// <summary>The command-line path, without a window. Results go to <paramref name="stdout"/>, messages to <paramref name="stderr"/>.</summary>
    internal static int Run(string[] args, TextWriter stdout, TextWriter stderr)
    {
        if (args.Length > 0 && args[0] == "--generate")
            return Generate(args.Skip(1).ToArray(), stdout, stderr);

        stderr.WriteLine(Usage);
        return 2;
    }

    private static int Generate(IReadOnlyList<string> paths, TextWriter stdout, TextWriter stderr)
    {
        if (paths.Count is < 1 or > Compositor.MaximumImages)
        {
            stderr.WriteLine($"Choose between one and {Compositor.MaximumImages} images.");
            return 1;
        }

        try
        {
            var (settings, warning) = SettingsStore.Load(AppPaths.SettingsFile);
            if (warning is not null)
                stderr.WriteLine(warning);

            var result = Compositor.Generate(paths, settings);
            stdout.WriteLine(result.OutputPath);
            return 0;
        }
        catch (SoftstageException ex)
        {
            stderr.WriteLine(ex.Message);
            return 1;
        }
        catch (Exception ex)
        {
            stderr.WriteLine($"Softstage could not create the composition. {ex.Message}");
            return 1;
        }
    }
}

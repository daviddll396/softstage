using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SkiaSharp;
using Softstage.Mac.Services;

namespace Softstage.Mac.Tests;

/// <summary>
/// A temporary folder plus injected settings and Services locations. Every file a test writes lives under Root.
/// Dispose restores the overrides and deletes Root.
/// </summary>
internal sealed class TestSandbox : IDisposable
{
    public TestSandbox()
    {
        Root = Path.Combine(Path.GetTempPath(), "softstage-tests", Guid.NewGuid().ToString("N"));
        Images = Path.Combine(Root, "images");
        SettingsFile = Path.Combine(Root, "preferences", "settings.json");
        ServicesFolder = Path.Combine(Root, "Services");
        Directory.CreateDirectory(Images);

        AppPaths.SettingsFileOverride = SettingsFile;
        FinderQuickAction.ServicesFolderOverride = ServicesFolder;
    }

    public string Root { get; }

    /// <summary>Where generated test images go. Output lands next to them, so this is the folder to inspect.</summary>
    public string Images { get; }

    public string SettingsFile { get; }

    public string ServicesFolder { get; }

    public void Dispose()
    {
        AppPaths.SettingsFileOverride = null;
        FinderQuickAction.ServicesFolderOverride = null;
        FinderQuickAction.IsMacOSOverride = null;

        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
            // A file still open in the test host. The temporary folder is left for the OS to clean up.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

/// <summary>Writes PNG test images with SkiaSharp, so the tests do not depend on git-tracked assets.</summary>
internal static class TestImages
{
    public const int PortraitWidth = 1086;
    public const int PortraitHeight = 1448;

    private static readonly SKColor[] Palette =
    [
        new(0xD9, 0x6B, 0x4A),
        new(0x3E, 0x7C, 0xB1),
        new(0x5B, 0xA5, 0x6B),
        new(0xE3, 0xB8, 0x3C),
        new(0x7A, 0x4E, 0x9C),
        new(0x2C, 0x2F, 0x36),
        new(0xF2, 0xEE, 0xE6),
        new(0x8C, 0xC7, 0xC1),
        new(0xB0, 0x3A, 0x48),
        new(0x4C, 0x5D, 0x78)
    ];

    /// <summary>Writes a portrait PNG. Even indexes are diagonal gradients, odd indexes are solid colours.</summary>
    public static string WritePng(string folder, string fileName, int index, int width = PortraitWidth, int height = PortraitHeight)
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);

        var first = Palette[index % Palette.Length];
        var second = Palette[(index + 3) % Palette.Length];

        using var bitmap = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint { IsAntialias = true })
        {
            if (index % 2 == 0)
                paint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(0, 0), new SKPoint(width, height), [first, second], null, SKShaderTileMode.Clamp);
            else
                paint.Color = first;

            canvas.DrawRect(new SKRect(0, 0, width, height), paint);
        }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
        return path;
    }
}

/// <summary>
/// Drives the window the way a user does. Button clicks raise the real Click event, so the XAML handlers run.
/// The file picker is the one step skipped: headless runs have no platform storage provider, so tests call AddImages directly.
/// </summary>
internal static class Ui
{
    public static MainWindow OpenWindow()
    {
        var window = new MainWindow();
        window.Show();
        return window;
    }

    public static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

    public static T Get<T>(MainWindow window, string name) where T : Control =>
        window.FindControl<T>(name) ?? throw new InvalidOperationException($"The window has no control named {name}.");

    public static string Status(MainWindow window) => Get<TextBlock>(window, "StatusText").Text ?? string.Empty;

    /// <summary>Pumps the UI thread until the condition holds. Background renders post their results back to this thread.</summary>
    public static void WaitUntil(Func<bool> condition, string description, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (true)
        {
            Dispatcher.UIThread.RunJobs();
            if (condition())
                return;

            if (DateTime.UtcNow >= deadline)
                throw new TimeoutException($"Timed out after {timeout.TotalSeconds:0} s waiting for: {description}.");

            Thread.Sleep(10);
        }
    }
}

/// <summary>The three tracked demo photos, if the test output folder sits inside the repository.</summary>
internal static class DemoImages
{
    private static readonly string[] Names = ["arch.png", "sphere.png", "stairway.png"];

    public static string[]? Find()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var folder = Path.Combine(directory.FullName, "assets", "site", "hero-r3");
            if (Names.All(name => File.Exists(Path.Combine(folder, name))))
                return Names.Select(name => Path.Combine(folder, name)).ToArray();
        }

        return null;
    }
}

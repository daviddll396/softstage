using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using SkiaSharp;
using Softstage.Core;
using Xunit;

namespace Softstage.Mac.Tests;

// Expected strings are the user-facing text in MainWindow.axaml.cs.
public sealed class MainWindowTests
{
    private const string LimitMessage = "The limit is 9 images. Remove an image to add more.";
    private const string UnsupportedMessage = "Choose PNG, JPEG, BMP, GIF, or TIFF files.";
    private const string CancelledMessage = "PNG creation cancelled. No new output was created.";
    private static readonly TimeSpan PreviewTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan CreateTimeout = TimeSpan.FromSeconds(120);

    [AvaloniaFact]
    public void Window_OpensWithNameAndTagline()
    {
        using var sandbox = new TestSandbox();
        var window = Ui.OpenWindow();
        try
        {
            Assert.Equal("Softstage", window.Title);

            var texts = window.GetVisualDescendants().OfType<TextBlock>().Select(text => text.Text).ToList();
            Assert.Contains("Softstage", texts);
            Assert.Contains("Compose on your device.", texts);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddImages_ThreeGeneratedPngs_ShowsCountAndPreviewBitmap()
    {
        using var sandbox = new TestSandbox();
        var paths = Enumerable.Range(0, 3)
            .Select(i => TestImages.WritePng(sandbox.Images, $"photo-{i + 1}.png", i))
            .ToArray();

        var window = Ui.OpenWindow();
        try
        {
            window.AddImages(paths);

            Assert.Equal("3/9", Ui.Get<TextBlock>(window, "ImageCountText").Text);
            Assert.Equal(3, Ui.Get<StackPanel>(window, "ImageList").Children.Count);
            Assert.Equal("3 images ready.", Ui.Status(window));

            var preview = Ui.Get<Image>(window, "PreviewImage");
            Ui.WaitUntil(() => preview.Source is Bitmap && preview.IsVisible, "a preview bitmap", PreviewTimeout);
            Assert.Equal("3 images · no cropping.", Ui.Get<TextBlock>(window, "PreviewCaption").Text);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddImages_TenthImage_IsRefusedAndCountStaysAtNine()
    {
        using var sandbox = new TestSandbox();
        var paths = Enumerable.Range(0, 10)
            .Select(i => TestImages.WritePng(sandbox.Images, $"photo-{i + 1:00}.png", i, 400, 500))
            .ToArray();

        var window = Ui.OpenWindow();
        try
        {
            window.AddImages(paths.Take(9));
            Assert.Equal("9/9", Ui.Get<TextBlock>(window, "ImageCountText").Text);

            window.AddImages(paths.Skip(9));

            Assert.Equal(LimitMessage, Ui.Status(window));
            Assert.Equal("9/9", Ui.Get<TextBlock>(window, "ImageCountText").Text);
            Assert.Equal(9, Ui.Get<StackPanel>(window, "ImageList").Children.Count);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AddImages_TextFile_IsRefusedAndNoImageIsAdded()
    {
        using var sandbox = new TestSandbox();
        var notes = Path.Combine(sandbox.Images, "notes.txt");
        File.WriteAllText(notes, "This is not an image.");

        var window = Ui.OpenWindow();
        try
        {
            window.AddImages(new[] { notes });

            Assert.Equal(UnsupportedMessage, Ui.Status(window));
            Assert.Equal("0/9", Ui.Get<TextBlock>(window, "ImageCountText").Text);
            Assert.Empty(Ui.Get<StackPanel>(window, "ImageList").Children);
            Assert.True(Ui.Get<TextBlock>(window, "EmptyListText").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CreatePng_ThreeImages_WritesCompositionNextToFirstImage()
    {
        using var sandbox = new TestSandbox();
        var paths = Enumerable.Range(0, 3)
            .Select(i => TestImages.WritePng(sandbox.Images, $"photo-{i + 1}.png", i))
            .ToArray();

        var window = Ui.OpenWindow();
        try
        {
            window.AddImages(paths);
            var create = Ui.Get<Button>(window, "CreatePngButton");
            Ui.Click(create);
            Ui.WaitUntil(() => create.IsEnabled, "the PNG to finish", CreateTimeout);

            var output = Path.Combine(sandbox.Images, "softstage-composition.png");
            Assert.True(File.Exists(output), "softstage-composition.png was not written next to the first image.");

            using var decoded = SKBitmap.Decode(output) ?? throw new InvalidDataException("The output is not a readable PNG.");
            Assert.Equal(1920, decoded.Width);
            Assert.Equal(1080, decoded.Height);
            Assert.Equal($"Created softstage-composition.png · {decoded.Width} × {decoded.Height} px.", Ui.Status(window));

            Assert.True(Ui.Get<Button>(window, "OpenImageButton").IsVisible);
            Assert.True(Ui.Get<Button>(window, "ShowFolderButton").IsVisible);

            // Only the three inputs and the output remain. Nothing else was left in the folder.
            Assert.Equal(4, Directory.GetFiles(sandbox.Images, "*.png").Length);
            Assert.DoesNotContain(Directory.EnumerateFiles(sandbox.Images),
                file => Path.GetFileName(file).StartsWith(".softstage-", StringComparison.Ordinal));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CreatePng_SingleImage_UsesStemWithSoftstageSuffix()
    {
        using var sandbox = new TestSandbox();
        var photo = TestImages.WritePng(sandbox.Images, "portrait.png", 0);

        var window = Ui.OpenWindow();
        try
        {
            window.AddImages(new[] { photo });
            var create = Ui.Get<Button>(window, "CreatePngButton");
            Ui.Click(create);
            Ui.WaitUntil(() => create.IsEnabled, "the PNG to finish", CreateTimeout);

            var output = Path.Combine(sandbox.Images, "portrait-softstage.png");
            Assert.True(File.Exists(output), "portrait-softstage.png was not written next to the image.");

            using var decoded = SKBitmap.Decode(output) ?? throw new InvalidDataException("The output is not a readable PNG.");
            Assert.Equal($"Created portrait-softstage.png · {decoded.Width} × {decoded.Height} px.", Ui.Status(window));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void CreatePng_CancelledStraightAway_LeavesNoOutputAndSaysSo()
    {
        using var sandbox = new TestSandbox();
        var paths = Enumerable.Range(0, 3)
            .Select(i => TestImages.WritePng(sandbox.Images, $"photo-{i + 1}.png", i))
            .ToArray();

        var window = Ui.OpenWindow();
        try
        {
            window.AddImages(paths);
            var create = Ui.Get<Button>(window, "CreatePngButton");
            var cancel = Ui.Get<Button>(window, "CancelButton");

            // The click handler runs up to its first await, so Cancel arrives while the render is starting or running.
            Ui.Click(create);
            Assert.True(cancel.IsVisible, "Cancel should be shown while the PNG is being created.");
            Ui.Click(cancel);

            Ui.WaitUntil(() => create.IsEnabled, "the cancelled PNG to settle", CreateTimeout);

            // Core has no cancel token, so the render may finish first. The UI then deletes the new file.
            Assert.Equal(CancelledMessage, Ui.Status(window));
            Assert.Equal(3, Directory.GetFiles(sandbox.Images, "*.png").Length);
            Assert.False(File.Exists(Path.Combine(sandbox.Images, "softstage-composition.png")));
            Assert.False(cancel.IsVisible);
            Assert.False(Ui.Get<Button>(window, "OpenImageButton").IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void SavePreferences_WritesSizeAndPadding_AndReloadsThem()
    {
        using var sandbox = new TestSandbox();
        var window = Ui.OpenWindow();
        try
        {
            Ui.Get<NumericUpDown>(window, "OutputWidthBox").Value = 2560;
            Ui.Get<NumericUpDown>(window, "PaddingBox").Value = 14;

            Ui.Click(Ui.Get<Button>(window, "SavePreferencesButton"));

            Assert.Equal("Preferences saved.", Ui.Status(window));
            Assert.True(File.Exists(sandbox.SettingsFile), "The settings file was not written to the injected path.");

            var (saved, warning) = SettingsStore.Load(sandbox.SettingsFile);
            Assert.Null(warning);
            Assert.Equal(2560, saved.OutputWidth);
            Assert.Equal(14, saved.PaddingPercent);
        }
        finally
        {
            window.Close();
        }

        var reopened = Ui.OpenWindow();
        try
        {
            Assert.Equal(2560m, Ui.Get<NumericUpDown>(reopened, "OutputWidthBox").Value);
            Assert.Equal(14m, Ui.Get<NumericUpDown>(reopened, "PaddingBox").Value);
        }
        finally
        {
            reopened.Close();
        }
    }

    [AvaloniaFact]
    public void SavePreferences_InvalidHexColor_ShowsErrorAndDoesNotSave()
    {
        using var sandbox = new TestSandbox();
        var window = Ui.OpenWindow();
        try
        {
            // Solid colour mode shows the base colour field.
            Ui.Get<ComboBox>(window, "BackgroundModeBox").SelectedIndex = 1;
            Ui.Get<TextBox>(window, "Color1Box").Text = "#GGGGGG";

            Ui.Click(Ui.Get<Button>(window, "SavePreferencesButton"));

            Assert.Equal("Enter a color in the format #RRGGBB.", Ui.Status(window));
            Assert.False(File.Exists(sandbox.SettingsFile), "An invalid colour must not be saved.");
        }
        finally
        {
            window.Close();
        }
    }
}

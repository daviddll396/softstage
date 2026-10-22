using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Platform.Storage;
using Softstage.Core;
using Softstage.Mac.Services;

namespace Softstage.Mac;

public partial class MainWindow : Window
{
    private static readonly TimeSpan PreviewDelay = TimeSpan.FromMilliseconds(250);
    private const int PreviewLongEdge = 1000;
    private static readonly Regex HexColor = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    // Labels follow the Windows app. Index order matches the enum values in Softstage.Core.
    private static readonly string[] RatioLabels = ["Auto", "Wide 16:9", "Square 1:1", "Portrait 4:5"];
    private static readonly string[] LayoutLabels = ["Auto", "Row", "Grid"];
    private static readonly string[] BackgroundModeLabels = ["Automatic gradient", "Solid color", "Two-color gradient", "Pattern"];
    private static readonly string[] PatternLabels = ["Soft grain", "Dots"];

    private readonly List<string> _imagePaths = [];
    private readonly SemaphoreSlim _renderGate = new(1, 1);
    private CancellationTokenSource? _previewCts;
    private CancellationTokenSource? _createCts;
    private Bitmap? _previewBitmap;
    private string? _lastOutputPath;

    // Last valid colors. Hidden color rows keep these, as the Windows dialog does.
    private string _color1Hex = "#303137";
    private string _color2Hex = "#4B4C53";

    public MainWindow()
    {
        InitializeComponent();
        Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://Softstage/Assets/app-icon.png")));

        RatioBox.ItemsSource = RatioLabels;
        LayoutBox.ItemsSource = LayoutLabels;
        BackgroundModeBox.ItemsSource = BackgroundModeLabels;
        PatternBox.ItemsSource = PatternLabels;

        var (settings, warning) = SettingsStore.Load(AppPaths.SettingsFile);
        ApplySettings(settings);

        // Wire change handlers after the initial values are set, so loading does not queue previews.
        RatioBox.SelectionChanged += (_, _) => OnSettingChanged();
        LayoutBox.SelectionChanged += (_, _) => OnSettingChanged();
        BackgroundModeBox.SelectionChanged += (_, _) => OnSettingChanged();
        PatternBox.SelectionChanged += (_, _) => OnSettingChanged();
        OutputWidthBox.ValueChanged += (_, _) => OnSettingChanged();
        PaddingBox.ValueChanged += (_, _) => OnSettingChanged();
        ShadowBox.ValueChanged += (_, _) => OnSettingChanged();
        LightenBox.ValueChanged += (_, _) => OnSettingChanged();
        Color1Box.TextChanged += (_, _) => OnSettingChanged();
        Color2Box.TextChanged += (_, _) => OnSettingChanged();

        ImagesPanel.AddHandler(DragDrop.DragOverEvent, OnDragOver);
        ImagesPanel.AddHandler(DragDrop.DropEvent, OnDrop);

        RebuildImageList();
        RefreshFinderState();
        SetStatus(warning ?? (_imagePaths.Count == 0 ? "Add images to begin. Preview updates as you edit." : "Images ready."));
        OnSettingChanged();
    }

    // ---- Settings <-> controls ----

    private void ApplySettings(AppSettings settings)
    {
        RatioBox.SelectedIndex = (int)settings.CanvasRatio;
        LayoutBox.SelectedIndex = (int)settings.Layout;
        OutputWidthBox.Value = settings.OutputWidth;
        PaddingBox.Value = settings.PaddingPercent;
        ShadowBox.Value = (decimal)Math.Round(settings.ShadowStrength * 100, 2);
        BackgroundModeBox.SelectedIndex = (int)settings.BackgroundMode;
        LightenBox.Value = settings.AutoGradientLightenPercent;
        _color1Hex = settings.BackgroundColor1Hex.ToUpperInvariant();
        _color2Hex = settings.BackgroundColor2Hex.ToUpperInvariant();
        Color1Box.Text = _color1Hex;
        Color2Box.Text = _color2Hex;
        PatternBox.SelectedIndex = (int)settings.BackgroundPattern;
        UpdateBackgroundFieldVisibility();
        UpdateSwatches();
    }

    /// <summary>Reads the controls. Returns false with a user-facing message when a value is not valid.</summary>
    private bool TryReadSettings(out AppSettings settings, out string error)
    {
        settings = new AppSettings();
        error = string.Empty;

        if (OutputWidthBox.Value is not { } width ||
            PaddingBox.Value is not { } padding ||
            ShadowBox.Value is not { } shadow ||
            LightenBox.Value is not { } lighten)
        {
            error = "Enter a number in each numeric field.";
            return false;
        }

        var mode = (BackgroundMode)Math.Max(0, BackgroundModeBox.SelectedIndex);
        var color1Visible = mode is BackgroundMode.SolidColor or BackgroundMode.CustomGradient or BackgroundMode.Pattern;
        var color2Visible = mode == BackgroundMode.CustomGradient;

        if (!TryReadColor(Color1Box, color1Visible, _color1Hex, out var color1) ||
            !TryReadColor(Color2Box, color2Visible, _color2Hex, out var color2))
        {
            error = "Enter a color in the format #RRGGBB.";
            return false;
        }

        settings = new AppSettings
        {
            OutputWidth = (int)width,
            PaddingPercent = (int)padding,
            ShadowStrength = (double)shadow / 100d,
            CanvasRatio = (CanvasRatio)Math.Max(0, RatioBox.SelectedIndex),
            Layout = (CompositionLayout)Math.Max(0, LayoutBox.SelectedIndex),
            BackgroundMode = mode,
            BackgroundColor1Hex = color1,
            BackgroundColor2Hex = color2,
            BackgroundPattern = (BackgroundPattern)Math.Max(0, PatternBox.SelectedIndex),
            AutoGradientLightenPercent = (int)lighten
        };

        if (!settings.IsValid())
        {
            error = "Check the values in the settings panel.";
            return false;
        }

        _color1Hex = color1;
        _color2Hex = color2;
        return true;
    }

    private static bool TryReadColor(TextBox box, bool isVisible, string fallback, out string hex)
    {
        hex = fallback;
        if (!isVisible)
            return true;

        var text = (box.Text ?? string.Empty).Trim().ToUpperInvariant();
        if (!HexColor.IsMatch(text))
            return false;

        hex = text;
        return true;
    }

    private void OnSettingChanged()
    {
        UpdateBackgroundFieldVisibility();
        UpdateSwatches();
        QueuePreview();
    }

    private void UpdateBackgroundFieldVisibility()
    {
        var mode = (BackgroundMode)Math.Max(0, BackgroundModeBox.SelectedIndex);
        LightenRow.IsVisible = mode == BackgroundMode.AutoGradient;
        Color1Row.IsVisible = mode is BackgroundMode.SolidColor or BackgroundMode.CustomGradient or BackgroundMode.Pattern;
        Color2Row.IsVisible = mode == BackgroundMode.CustomGradient;
        PatternRow.IsVisible = mode == BackgroundMode.Pattern;
    }

    private void UpdateSwatches()
    {
        UpdateSwatch(Color1Box, Color1Swatch);
        UpdateSwatch(Color2Box, Color2Swatch);
    }

    private void UpdateSwatch(TextBox box, Border swatch)
    {
        var text = (box.Text ?? string.Empty).Trim();
        swatch.Background = HexColor.IsMatch(text) && Color.TryParse(text, out var color)
            ? new SolidColorBrush(color)
            : (IBrush)this.FindResource("FieldBrush")!;
    }

    // ---- Preview ----

    /// <summary>Restarts the debounce. The newest request wins; older renders are discarded when they finish.</summary>
    private void QueuePreview()
    {
        _previewCts?.Cancel();
        _previewCts = null;

        if (_imagePaths.Count == 0)
        {
            ShowPreviewPlaceholder("A sample composition will appear here.");
            return;
        }

        if (!TryReadSettings(out var settings, out var error))
        {
            ShowPreviewPlaceholder($"Preview is paused. {error}");
            return;
        }

        var cts = new CancellationTokenSource();
        _previewCts = cts;
        _ = RenderPreviewAsync(_imagePaths.ToArray(), settings, cts.Token);
    }

    private async Task RenderPreviewAsync(string[] paths, AppSettings settings, CancellationToken token)
    {
        PreviewCaption.Text = "Updating preview…";
        try
        {
            await Task.Delay(PreviewDelay, token);

            await _renderGate.WaitAsync(token);
            byte[] bytes;
            try
            {
                bytes = await Task.Run(() => Compositor.RenderPng(paths, settings, PreviewLongEdge));
            }
            finally
            {
                _renderGate.Release();
            }

            if (!token.IsCancellationRequested)
                ShowPreview(bytes, paths.Length);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested)
        {
            // A newer change replaced this request.
        }
        catch (SoftstageException ex) when (!token.IsCancellationRequested)
        {
            ShowPreviewPlaceholder($"Preview could not be updated. {ex.Message}");
        }
        catch (Exception) when (!token.IsCancellationRequested)
        {
            ShowPreviewPlaceholder("Preview could not be updated. Check that the selected images are valid and available.");
        }
    }

    private void ShowPreview(byte[] bytes, int imageCount)
    {
        using var stream = new MemoryStream(bytes);
        var bitmap = new Bitmap(stream);

        var previous = _previewBitmap;
        _previewBitmap = bitmap;
        PreviewImage.Source = bitmap;
        PreviewImage.IsVisible = true;
        PreviewPlaceholder.IsVisible = false;
        PreviewCaption.Text = $"{Plural(imageCount, "image")} · no cropping.";
        previous?.Dispose();
    }

    private void ShowPreviewPlaceholder(string message)
    {
        var previous = _previewBitmap;
        _previewBitmap = null;
        PreviewImage.Source = null;
        previous?.Dispose();

        PreviewImage.IsVisible = false;
        PreviewPlaceholder.IsVisible = true;
        PreviewPlaceholder.Text = message;
        PreviewCaption.Text = string.Empty;
    }

    // ---- Images ----

    private async void OnAddImagesClick(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Add images to the composition",
            AllowMultiple = true,
            FileTypeFilter = [ImageFiles.PickerFilter]
        });

        AddImages(files.Select(file => file.TryGetLocalPath()).OfType<string>());
    }

    private void OnDragOver(object? sender, DragEventArgs e)
    {
        var hasFiles = e.DataTransfer.Contains(DataFormat.File);
        e.DragEffects = hasFiles ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        var files = e.DataTransfer.TryGetFiles();
        if (files is not null)
            AddImages(files.Select(file => file.TryGetLocalPath()).OfType<string>());

        e.Handled = true;
    }

    private void AddImages(IEnumerable<string> candidates)
    {
        var incoming = candidates.ToList();
        if (incoming.Count == 0)
            return;

        var supported = incoming.Where(ImageFiles.IsSupported).ToList();
        var unsupportedCount = incoming.Count - supported.Count;
        var fresh = supported
            .Distinct(StringComparer.Ordinal)
            .Where(path => !_imagePaths.Contains(path, StringComparer.Ordinal))
            .ToList();
        var alreadySelected = supported.Count - fresh.Count;

        if (fresh.Count > 0 && _imagePaths.Count + fresh.Count > Compositor.MaximumImages)
        {
            var room = Math.Max(0, Compositor.MaximumImages - _imagePaths.Count);
            SetStatus(room == 0
                ? $"The limit is {Compositor.MaximumImages} images. Remove an image to add more."
                : $"The limit is {Compositor.MaximumImages} images. You can add {Plural(room, "more image", "more images")}.");
            return;
        }

        if (fresh.Count == 0 && supported.Count == 0)
        {
            SetStatus("Choose PNG, JPEG, BMP, GIF, or TIFF files.");
            return;
        }

        _imagePaths.AddRange(fresh);
        RebuildImageList();
        QueuePreview();

        var parts = new List<string>();
        if (unsupportedCount > 0)
            parts.Add($"Skipped {Plural(unsupportedCount, "file", "files")} that are not supported images.");
        if (alreadySelected > 0 && fresh.Count == 0)
            parts.Add("Those images are already selected.");
        else if (alreadySelected > 0)
            parts.Add($"{Plural(alreadySelected, "image")} already selected.");
        if (_imagePaths.Count > 0)
            parts.Add($"{_imagePaths.Count} {(_imagePaths.Count == 1 ? "image" : "images")} ready.");

        SetStatus(string.Join(" ", parts));
    }

    private void RemoveImage(string path)
    {
        _imagePaths.Remove(path);
        RebuildImageList();
        QueuePreview();
        SetStatus(_imagePaths.Count == 0
            ? "Add images to begin. Preview updates as you edit."
            : $"{_imagePaths.Count} {(_imagePaths.Count == 1 ? "image" : "images")} ready.");
    }

    private void OnClearImagesClick(object? sender, RoutedEventArgs e)
    {
        _imagePaths.Clear();
        RebuildImageList();
        QueuePreview();
        SetStatus("Image list cleared.");
    }

    private void RebuildImageList()
    {
        ImageList.Children.Clear();
        foreach (var path in _imagePaths)
        {
            var name = new TextBlock
            {
                Text = Path.GetFileName(path),
                TextTrimming = Avalonia.Media.TextTrimming.CharacterEllipsis,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            ToolTip.SetTip(name, path);

            var remove = new Button
            {
                Content = "Remove",
                FontSize = 13,
                Padding = new Thickness(10, 4),
                MinHeight = 28
            };
            var capturedPath = path;
            remove.Click += (_, _) => RemoveImage(capturedPath);

            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 8 };
            row.Children.Add(name);
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);

            ImageList.Children.Add(new Border
            {
                Background = (IBrush)this.FindResource("SurfaceBrush")!,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6),
                Child = row
            });
        }

        EmptyListText.IsVisible = _imagePaths.Count == 0;
        ImageCountText.Text = $"{_imagePaths.Count}/{Compositor.MaximumImages}";
        ClearImagesButton.IsEnabled = _imagePaths.Count > 0;
    }

    // ---- Preferences ----

    private void OnSavePreferencesClick(object? sender, RoutedEventArgs e)
    {
        if (!TryReadSettings(out var settings, out var error))
        {
            SetStatus(error);
            return;
        }

        try
        {
            SettingsStore.Save(settings, AppPaths.SettingsFile);
            SetStatus("Preferences saved.");
        }
        catch (SoftstageException ex)
        {
            SetStatus(ex.Message);
        }
        catch (Exception ex)
        {
            SetStatus($"Preferences could not be saved. {ex.Message}");
        }
    }

    // ---- PNG creation ----

    private async void OnCreatePngClick(object? sender, RoutedEventArgs e)
    {
        if (_imagePaths.Count == 0)
        {
            SetStatus("Add images before creating a PNG.");
            return;
        }

        if (!TryReadSettings(out var settings, out var error))
        {
            SetStatus(error);
            return;
        }

        var paths = _imagePaths.ToArray();
        var cts = new CancellationTokenSource();
        _createCts = cts;
        SetCreating(true);
        SetResultButtons(false);
        SetStatus("Creating PNG…");

        try
        {
            var result = await Task.Run(() => Compositor.Generate(paths, settings), cts.Token);
            if (cts.IsCancellationRequested)
            {
                // Generate cannot be stopped midway. The new file is removed so a cancelled run leaves nothing behind.
                DeleteQuietly(result.OutputPath);
                SetStatus("PNG creation cancelled. No new output was created.");
            }
            else
            {
                _lastOutputPath = result.OutputPath;
                SetStatus($"Created {Path.GetFileName(result.OutputPath)} · {result.Width} × {result.Height} px.");
                SetResultButtons(true);
            }
        }
        catch (OperationCanceledException)
        {
            SetStatus("PNG creation cancelled. No new output was created.");
        }
        catch (SoftstageException ex)
        {
            SetStatus(ex.Message);
        }
        catch (Exception)
        {
            SetStatus("Could not create the PNG. Check that the selected images are available and that the first image's folder is writable.");
        }
        finally
        {
            _createCts = null;
            SetCreating(false);
            cts.Dispose();
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        if (_createCts is null || _createCts.IsCancellationRequested)
            return;

        _createCts.Cancel();
        CancelButton.IsEnabled = false;
        SetStatus("Cancelling PNG creation…");
    }

    private void SetCreating(bool creating)
    {
        CreatePngButton.IsEnabled = !creating;
        CreatePngButton.Content = creating ? "Creating PNG…" : "Create PNG";
        CancelButton.IsVisible = creating;
        CancelButton.IsEnabled = true;
    }

    private void SetResultButtons(bool visible)
    {
        OpenImageButton.IsVisible = visible;
        ShowFolderButton.IsVisible = visible;
    }

    private void OnOpenImageClick(object? sender, RoutedEventArgs e)
    {
        if (_lastOutputPath is null)
            return;

        try
        {
            SystemOpener.OpenFile(_lastOutputPath);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not open the image. {ex.Message}");
        }
    }

    private void OnShowFolderClick(object? sender, RoutedEventArgs e)
    {
        if (_lastOutputPath is null)
            return;

        try
        {
            SystemOpener.ShowInFolder(_lastOutputPath);
        }
        catch (Exception ex)
        {
            SetStatus($"Could not open the selected location. {ex.Message}");
        }
    }

    private static void DeleteQuietly(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Nothing else to do; the status line already says no output was kept.
        }
    }

    // ---- Finder action ----

    private void OnFinderToggleClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (FinderQuickAction.IsInstalled)
            {
                FinderQuickAction.Remove();
            }
            else
            {
                var executable = Environment.ProcessPath
                    ?? throw new SoftstageException("Softstage could not find its own executable path.");
                FinderQuickAction.Install(executable);
            }

            RefreshFinderState();
        }
        catch (SoftstageException ex)
        {
            FinderStatusText.Text = ex.Message;
        }
        catch (Exception ex)
        {
            FinderStatusText.Text = $"The Finder action could not be changed. {ex.Message}";
        }
    }

    private void RefreshFinderState()
    {
        if (!OperatingSystem.IsMacOS())
        {
            FinderToggleButton.IsEnabled = false;
            FinderToggleButton.Content = "Enable Finder right-click";
            FinderStatusText.Text = "Finder right-click is available on macOS only.";
            return;
        }

        var installed = FinderQuickAction.IsInstalled;
        FinderToggleButton.IsEnabled = true;
        FinderToggleButton.Content = installed ? "Remove Finder right-click" : "Enable Finder right-click";
        FinderStatusText.Text = installed ? "Finder right-click is on." : "Finder right-click is off.";
    }

    // ---- Helpers ----

    private void SetStatus(string text) => StatusText.Text = text;

    private static string Plural(int count, string singular, string? plural = null) =>
        $"{count} {(count == 1 ? singular : plural ?? singular + "s")}";

    protected override void OnClosed(EventArgs e)
    {
        _previewCts?.Cancel();
        _previewCts = null;
        _previewBitmap?.Dispose();
        _previewBitmap = null;
        base.OnClosed(e);
    }
}

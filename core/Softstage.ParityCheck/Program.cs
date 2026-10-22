using System.Diagnostics;
using System.Runtime.InteropServices;
using SkiaSharp;
using Softstage.Core;

namespace Softstage.ParityCheck;

internal static class Program
{
    private const string Root = @"C:\work\job helper\projects\softstage";

    // Pass thresholds. Fixed before the first run and not changed to make the run pass.
    //  - Whole canvas and image rectangles: mean absolute error (per channel) under 2.0 levels and 99th
    //    percentile under 16 levels. The expected differences are resampling and rounding in photographic
    //    content. A wrong layout or wrong sampler produces large edge errors and fails these limits.
    //  - Background only: mean under 0.5 levels and 99th percentile at most 1. This region has no resampling
    //    and no shadow. The gradient colours and dither are integer-exact, so only float rounding could differ.
    //  - Geometry: within 0.01 px of the Windows numbers. Those numbers are given to two decimals.
    //  - Gradient endpoints: exact match to #24325D (left) and #DCA47B (right).
    private const double WholeMaeLimit = 2.0;
    private const int WholeP99Limit = 16;
    private const double BackgroundMaeLimit = 0.5;
    private const int BackgroundP99Limit = 1;
    private const double GeometryTolerance = 0.01;

    private static readonly string[] InputNames = { "arch.png", "stairway.png", "sphere.png" };
    private static readonly string AssetDirectory = Path.Combine(Root, "assets", "site", "hero-r3");
    private static readonly string ReferencePath = Path.Combine(Root, "artifacts", "composition-check", "softstage-composition.png");
    private static readonly string ParityDirectory = Path.Combine(Root, "artifacts", "core-parity");
    private static readonly string OutputDirectory = Path.Combine(ParityDirectory, "out");

    private static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "sweep")
            return Sweep();
        if (args.Length > 0 && args[0] == "probe")
            return Probe(args.Skip(1).ToArray());

        var ok = RunParity();
        ok = SettingsChecks.Run() && ok;
        ok = ExifChecks.Run(OutputDirectory) && ok;
        Console.WriteLine(ok ? "OVERALL: PASS" : "OVERALL: FAIL");
        return ok ? 0 : 1;
    }

    private static AppSettings ParitySettings() => new()
    {
        OutputWidth = 3840,
        AutoGradientLightenPercent = 0
    };

    private static bool RunParity()
    {
        Console.WriteLine("== Parity: Softstage.Core vs Windows reference ==");
        if (Directory.Exists(OutputDirectory))
            Directory.Delete(OutputDirectory, recursive: true);
        var inputDirectory = Path.Combine(OutputDirectory, "inputs");
        Directory.CreateDirectory(inputDirectory);

        // Copies go to the output folder so the repo asset folder is not written to.
        var inputs = InputNames
            .Select(name =>
            {
                var copy = Path.Combine(inputDirectory, name);
                File.Copy(Path.Combine(AssetDirectory, name), copy, overwrite: true);
                return copy;
            })
            .ToArray();

        var settings = ParitySettings();
        Console.WriteLine($"Settings: OutputWidth={settings.OutputWidth} PaddingPercent={settings.PaddingPercent} " +
                          $"ShadowStrength={settings.ShadowStrength} CanvasRatio={settings.CanvasRatio} Layout={settings.Layout} " +
                          $"BackgroundMode={settings.BackgroundMode} AutoGradientLightenPercent={settings.AutoGradientLightenPercent}");
        Console.WriteLine($"SkiaSharp {typeof(SKImage).Assembly.GetName().Version}, resampler: Mitchell (B=1/3, C=1/3)");

        var ok = true;
        var stopwatch = Stopwatch.StartNew();
        var generated = Compositor.Generate(inputs, settings);
        stopwatch.Stop();
        Console.WriteLine($"Generate -> {generated.OutputPath} ({generated.Width}x{generated.Height}) in {stopwatch.ElapsedMilliseconds} ms");

        // Naming rules: a second run gets the -2 suffix, and one image gets "<stem>-softstage".
        var second = Compositor.Generate(inputs, settings);
        ok &= Check("naming: second run adds -2 suffix", Path.GetFileName(second.OutputPath) == "softstage-composition-2.png");
        var single = Compositor.Generate(new[] { inputs[0] }, settings);
        ok &= Check("naming: one image gives <stem>-softstage.png", Path.GetFileName(single.OutputPath) == "arch-softstage.png");

        // Preview path: long edge capped at 1000 px.
        var previewBytes = Compositor.RenderPng(inputs, settings, 1000);
        File.WriteAllBytes(Path.Combine(OutputDirectory, "preview-1000.png"), previewBytes);
        var previewInfo = SKBitmap.DecodeBounds(Path.Combine(OutputDirectory, "preview-1000.png"));
        var previewWidth = previewInfo.Width;
        var previewHeight = previewInfo.Height;
        ok &= Check($"preview 1000 px long edge -> {previewWidth}x{previewHeight} (expected 1000x562)", previewWidth == 1000 && previewHeight == 562);

        var output = ReadBgra(generated.OutputPath, out var width, out var height);
        var reference = ReadBgra(ReferencePath, out var referenceWidth, out var referenceHeight);
        Console.WriteLine($"Output: {width}x{height}  Reference: {referenceWidth}x{referenceHeight}");
        ok &= Check("dimensions match reference 3840x2160", width == referenceWidth && height == referenceHeight && width == 3840 && height == 2160);

        var loaded = ImageLoader.Load(inputs, null, Compositor.DefaultSampling);
        var sizes = loaded.Select(image => new ImageSize(image.Width, image.Height)).ToArray();
        Console.WriteLine("Inputs: " + string.Join(", ", loaded.Select(image => $"{image.Width}x{image.Height}")));
        var geometry = CanvasLayout.Compute(sizes, settings);
        ok &= CheckGeometry(geometry);

        var (startColor, endColor) = Background.DominantColors(loaded, settings.AutoGradientLightenPercent);
        var expectedFirst = Background.ParseHex("#24325D");
        var expectedSecond = Background.ParseHex("#DCA47B");
        Console.WriteLine($"Gradient endpoints (ours): #{startColor.R:X2}{startColor.G:X2}{startColor.B:X2} and #{endColor.R:X2}{endColor.G:X2}{endColor.B:X2}");
        ok &= Check("gradient endpoints equal #24325D and #DCA47B", startColor == expectedFirst && endColor == expectedSecond);

        var outputLeft = Pixel(output, width, 0, 0);
        var outputRight = Pixel(output, width, width - 1, 0);
        ok &= Check("output pixel (0,0) equals #24325D exactly", outputLeft == expectedFirst);
        ok &= Check("output pixel (W-1,0) equals #DCA47B exactly", outputRight == expectedSecond);

        var padding = ShadowRenderer.Padding(geometry.Width);
        var report = Metrics.Compare(output, reference, width, height, geometry, padding);
        Console.WriteLine($"Shadow padding (px): {padding}");
        Console.WriteLine("Error statistics (channel values 0-255; p99 pooled over R, G, B; > = share of pixels whose largest channel error exceeds the limit):");
        foreach (var region in report.All)
            Metrics.Print(region);

        var wholeOk = report.Whole.WorstMae < WholeMaeLimit && report.Whole.P99() < WholeP99Limit;
        var imagesOk = report.AllImages.WorstMae < WholeMaeLimit && report.AllImages.P99() < WholeP99Limit;
        var backgroundOk = report.Background.WorstMae < BackgroundMaeLimit && report.Background.P99() <= BackgroundP99Limit;
        ok &= Check($"whole canvas: worst MAE {report.Whole.WorstMae:F3} < {WholeMaeLimit} and p99 {report.Whole.P99()} < {WholeP99Limit}", wholeOk);
        ok &= Check($"image rectangles: worst MAE {report.AllImages.WorstMae:F3} < {WholeMaeLimit} and p99 {report.AllImages.P99()} < {WholeP99Limit}", imagesOk);
        ok &= Check($"background only: worst MAE {report.Background.WorstMae:F3} < {BackgroundMaeLimit} and p99 {report.Background.P99()} <= {BackgroundP99Limit}", backgroundOk);

        var sideBySide = Path.Combine(ParityDirectory, "side-by-side.png");
        var heatmap = Path.Combine(ParityDirectory, "diff-heatmap.png");
        SaveSideBySide(reference, output, width, height, sideBySide);
        SaveHeatmap(report.MaxChannelDifference, width, height, heatmap);
        Console.WriteLine($"Saved {sideBySide} (left reference, right output, half size)");
        Console.WriteLine($"Saved {heatmap} (max channel error per pixel: black 0, red about 5, yellow about 11, white 16 or more)");

        Console.WriteLine(ok ? "PARITY: PASS" : "PARITY: FAIL");
        return ok;
    }

    private static bool CheckGeometry(CanvasGeometry geometry)
    {
        var b = geometry.ImageBounds;
        var rows = new (string Name, double Ours, double Expected, double Tolerance)[]
        {
            ("canvas width", geometry.Width, 3840, 0),
            ("canvas height", geometry.Height, 2160, 0),
            ("image height", b[0].Height, 1318.45, GeometryTolerance),
            ("gap", b[1].Left - b[0].Right, 52.74, GeometryTolerance),
            ("image width", b[0].Width, 988.84, GeometryTolerance),
            ("top", b[0].Top, 420.77, GeometryTolerance),
            ("x position 1", b[0].Left, 384, GeometryTolerance),
            ("x position 2", b[1].Left, 1425.58, GeometryTolerance),
            ("x position 3", b[2].Left, 2467.16, GeometryTolerance)
        };

        var ok = true;
        Console.WriteLine("Geometry (ours vs Windows reference):");
        foreach (var row in rows)
        {
            var delta = Math.Abs(row.Ours - row.Expected);
            var pass = delta <= row.Tolerance + 1e-9;
            Console.WriteLine($"  {row.Name,-14} ours={row.Ours,10:F4}  expected={row.Expected,10:F2}  |diff|={delta:F4}  {(pass ? "ok" : "MISMATCH")}");
            ok &= pass;
        }
        return Check("geometry matches Windows numbers within 0.01 px", ok);
    }

    private static bool Check(string description, bool pass)
    {
        Console.WriteLine($"[{(pass ? "PASS" : "FAIL")}] {description}");
        return pass;
    }

    private static Rgb Pixel(byte[] bgra, int width, int x, int y)
    {
        var p = (y * width + x) * 4;
        return new Rgb(bgra[p + 2], bgra[p + 1], bgra[p]);
    }

    /// <summary>Decodes a PNG to unpremultiplied BGRA and reports whether any pixel is not opaque.</summary>
    private static byte[] ReadBgra(string path, out int width, out int height)
    {
        using var codec = SKCodec.Create(path) ?? throw new InvalidOperationException($"Could not open {path}.");
        width = codec.Info.Width;
        height = codec.Info.Height;
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        using var bitmap = SKBitmap.Decode(path, info) ?? throw new InvalidOperationException($"Could not decode {path}.");
        var bytes = new byte[width * height * 4];
        Marshal.Copy(bitmap.GetPixels(), bytes, 0, bytes.Length);
        return bytes;
    }

    private static void SaveSideBySide(byte[] reference, byte[] output, int width, int height, string path)
    {
        var halfWidth = width / 2;
        var halfHeight = height / 2;
        using var referenceImage = PixelBuffer.CreateImage(width, height, reference, SKAlphaType.Unpremul);
        using var outputImage = PixelBuffer.CreateImage(width, height, output, SKAlphaType.Unpremul);
        using var bitmap = new SKBitmap(new SKImageInfo(halfWidth * 2 + 8, halfHeight, SKColorType.Bgra8888, SKAlphaType.Opaque));
        using (var canvas = new SKCanvas(bitmap))
        {
            var sampling = new SKSamplingOptions(SKCubicResampler.Mitchell);
            canvas.Clear(new SKColor(20, 20, 20));
            canvas.DrawImage(referenceImage, new SKRect(0, 0, width, height), new SKRect(0, 0, halfWidth, halfHeight), sampling);
            canvas.DrawImage(outputImage, new SKRect(0, 0, width, height), new SKRect(halfWidth + 8, 0, halfWidth * 2 + 8, halfHeight), sampling);
        }
        SavePng(bitmap, path);
    }

    private static void SaveHeatmap(byte[] maxDifference, int width, int height, string path)
    {
        var bgra = new byte[width * height * 4];
        for (var i = 0; i < maxDifference.Length; i++)
        {
            var t = Math.Min(1.0, maxDifference[i] / 16.0);
            var r = Math.Clamp(t * 3, 0, 1);
            var g = Math.Clamp((t - 1.0 / 3) * 3, 0, 1);
            var b = Math.Clamp((t - 2.0 / 3) * 3, 0, 1);
            bgra[i * 4] = (byte)(b * 255);
            bgra[i * 4 + 1] = (byte)(g * 255);
            bgra[i * 4 + 2] = (byte)(r * 255);
            bgra[i * 4 + 3] = 255;
        }
        using var image = PixelBuffer.CreateImage(width, height, bgra, SKAlphaType.Opaque);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    private static void SavePng(SKBitmap bitmap, string path)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }

    /// <summary>
    /// Resampler sweep (manual use). Renders the parity case with each candidate and reports error on the
    /// image rectangles. The best candidate becomes Compositor.DefaultSampling.
    /// </summary>
    private static int Sweep()
    {
        var inputs = InputNames.Select(name => Path.Combine(AssetDirectory, name)).ToArray();
        var reference = ReadBgra(ReferencePath, out var width, out var height);
        var settings = ParitySettings();
        var geometry = CanvasLayout.Compute(
            ImageLoader.Load(inputs, null, Compositor.DefaultSampling).Select(i => new ImageSize(i.Width, i.Height)).ToArray(),
            settings);
        var padding = ShadowRenderer.Padding(geometry.Width);

        var candidates = new (string Name, SKSamplingOptions Sampling)[]
        {
            ("Mitchell B=1/3 C=1/3", new SKSamplingOptions(SKCubicResampler.Mitchell)),
            ("CatmullRom B=0 C=1/2", new SKSamplingOptions(SKCubicResampler.CatmullRom)),
            ("Cubic B=0 C=0.75", new SKSamplingOptions(new SKCubicResampler(0f, 0.75f))),
            ("B-spline B=1 C=0", new SKSamplingOptions(new SKCubicResampler(1f, 0f))),
            ("Linear (non-cubic baseline)", new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None))
        };

        Console.WriteLine("== Resampler sweep (export size, 3840 px) ==");
        Console.WriteLine($"{"candidate",-30} {"image MAE",10} {"image p99",10} {"whole MAE",10} {"whole p99",10} {"bg MAE",8}");
        var best = (Name: "", Score: double.MaxValue, Sampling: default(SKSamplingOptions));
        foreach (var (name, sampling) in candidates)
        {
            using var result = Compositor.Render(inputs, settings, null, 384, sampling);
            var output = new byte[width * height * 4];
            Marshal.Copy(result.Bitmap.GetPixels(), output, 0, output.Length);
            var report = Metrics.Compare(output, reference, width, height, geometry, padding);
            var imageScore = report.AllImages.MeanMae;
            Console.WriteLine($"{name,-30} {imageScore,10:F4} {report.AllImages.P99(),10} {report.Whole.MeanMae,10:F4} {report.Whole.P99(),10} {report.Background.MeanMae,8:F4}");
            if (imageScore < best.Score)
                best = (name, imageScore, sampling);
        }
        Console.WriteLine($"Best by image-rectangle mean MAE: {best.Name} ({best.Score:F4})");
        return 0;
    }

    /// <summary>Decode probe for individual files (manual use): prints the size or the user-facing error.</summary>
    private static int Probe(string[] files)
    {
        var failed = false;
        foreach (var file in files)
        {
            try
            {
                var images = ImageLoader.Load(new[] { file }, null, Compositor.DefaultSampling);
                Console.WriteLine($"OK      {Path.GetFileName(file)}: {images[0].Width}x{images[0].Height}");
            }
            catch (SoftstageException ex)
            {
                failed = true;
                Console.WriteLine($"ERROR   {Path.GetFileName(file)}: {ex.Message}");
            }
        }
        return failed ? 1 : 0;
    }
}

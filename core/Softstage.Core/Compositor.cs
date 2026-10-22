using System.Runtime.InteropServices;
using SkiaSharp;

namespace Softstage.Core;

public static class Compositor
{
    public const int MaximumImages = 9;

    private const int ExportShadowMaskLongEdge = 384;
    private const int PreviewShadowMaskLongEdge = 256;

    /// <summary>Resampler for image drawing and shadow upscaling. Chosen by the parity sweep.</summary>
    internal static readonly SKSamplingOptions DefaultSampling = new(SKCubicResampler.Mitchell);

    /// <summary>
    /// Renders the composition at full output width and writes it as PNG next to the first image.
    /// One image: "&lt;stem&gt;-softstage.png". Several: "softstage-composition.png". Name clashes get -2, -3, and so on.
    /// </summary>
    public static GenerateResult Generate(IReadOnlyList<string> imagePaths, AppSettings settings)
    {
        using var result = Render(imagePaths, settings, null, ExportShadowMaskLongEdge, DefaultSampling);
        var bytes = EncodePng(result.Bitmap);

        var folder = Path.GetDirectoryName(Path.GetFullPath(imagePaths[0]))!;
        var baseName = imagePaths.Count == 1
            ? $"{Path.GetFileNameWithoutExtension(imagePaths[0])}-softstage"
            : "softstage-composition";

        for (var suffix = 1; ; suffix++)
        {
            var name = suffix == 1 ? $"{baseName}.png" : $"{baseName}-{suffix}.png";
            var destination = Path.Combine(folder, name);
            if (File.Exists(destination))
                continue;

            var temporary = Path.Combine(folder, $".softstage-{Guid.NewGuid():N}.tmp");
            var temporaryCreated = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan))
                {
                    temporaryCreated = true;
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(flushToDisk: true);
                }

                try
                {
                    File.Move(temporary, destination);
                }
                catch (IOException) when (File.Exists(destination))
                {
                    continue;
                }

                return new GenerateResult(destination, result.Width, result.Height);
            }
            finally
            {
                if (temporaryCreated)
                    try { File.Delete(temporary); } catch { }
            }
        }
    }

    /// <summary>
    /// Renders the composition to PNG bytes in memory. When <paramref name="maxLongEdge"/> is set, the
    /// output long edge is capped and the inputs are downscaled first, as the Windows preview does.
    /// </summary>
    public static byte[] RenderPng(IReadOnlyList<string> imagePaths, AppSettings settings, int? maxLongEdge = null)
    {
        if (maxLongEdge is < 1)
            throw new ArgumentOutOfRangeException(nameof(maxLongEdge), "The maximum long edge must be at least 1 pixel.");

        var shadowMaskLongEdge = maxLongEdge is null ? ExportShadowMaskLongEdge : PreviewShadowMaskLongEdge;
        using var result = Render(imagePaths, settings, maxLongEdge, shadowMaskLongEdge, DefaultSampling);
        return EncodePng(result.Bitmap);
    }

    internal static RenderResult Render(
        IReadOnlyList<string> imagePaths,
        AppSettings settings,
        int? maxLongEdge,
        int shadowMaskLongEdge,
        SKSamplingOptions sampling)
    {
        ValidateRequest(imagePaths, settings);

        var effective = settings.Copy();
        if (maxLongEdge is { } cap)
            effective.OutputWidth = Math.Min(effective.OutputWidth, cap);

        var images = ImageLoader.Load(imagePaths, maxLongEdge, sampling);
        var geometry = CanvasLayout.Compute(images.Select(image => new ImageSize(image.Width, image.Height)).ToArray(), effective);
        var pixels = Background.Create(geometry.Width, geometry.Height, images, effective);

        var bitmap = new SKBitmap(new SKImageInfo(geometry.Width, geometry.Height, SKColorType.Bgra8888, SKAlphaType.Premul));
        try
        {
            Marshal.Copy(pixels, 0, bitmap.GetPixels(), pixels.Length);
            using (var canvas = new SKCanvas(bitmap))
            {
                for (var i = 0; i < images.Count; i++)
                {
                    var bounds = geometry.ImageBounds[i];
                    ShadowRenderer.Draw(canvas, bounds, effective.ShadowStrength, geometry.Width, shadowMaskLongEdge, sampling);

                    var radius = Math.Min(2f * Math.Max(geometry.Width, geometry.Height) / 1920f,
                        Math.Min(bounds.Width, bounds.Height) / 2);
                    using var clip = CanvasLayout.RoundedPath(bounds, radius);
                    using var source = images[i].ToImage();
                    canvas.Save();
                    canvas.ClipPath(clip, SKClipOperation.Intersect, antialias: true);
                    canvas.DrawImage(source, new SKRect(0, 0, source.Width, source.Height), bounds.ToSk(), sampling);
                    canvas.Restore();
                }
            }
            return new RenderResult(bitmap, geometry.Width, geometry.Height);
        }
        catch
        {
            bitmap.Dispose();
            throw;
        }
    }

    private static void ValidateRequest(IReadOnlyList<string> imagePaths, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(imagePaths);
        ArgumentNullException.ThrowIfNull(settings);
        if (imagePaths.Count is < 1 or > MaximumImages)
            throw new SoftstageException($"Choose between one and {MaximumImages} images.");
        if (!settings.IsValid())
            throw new SoftstageException("Settings are outside the supported range.");
    }

    private static byte[] EncodePng(SKBitmap bitmap)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }
}

internal sealed class RenderResult : IDisposable
{
    public RenderResult(SKBitmap bitmap, int width, int height)
    {
        Bitmap = bitmap;
        Width = width;
        Height = height;
    }

    public SKBitmap Bitmap { get; }
    public int Width { get; }
    public int Height { get; }

    public void Dispose() => Bitmap.Dispose();
}

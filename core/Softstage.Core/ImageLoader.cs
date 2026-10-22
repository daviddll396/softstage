using System.Runtime.InteropServices;
using SkiaSharp;

namespace Softstage.Core;

/// <summary>Unpremultiplied BGRA pixels, already oriented.</summary>
internal sealed class PixelBuffer
{
    public PixelBuffer(int width, int height, byte[] bgra)
    {
        Width = width;
        Height = height;
        Bgra = bgra;
    }

    public int Width { get; }
    public int Height { get; }
    public byte[] Bgra { get; }

    /// <summary>An immutable image holding a copy of the pixels, for drawing with a sampler.</summary>
    public SKImage ToImage() => CreateImage(Width, Height, Bgra, SKAlphaType.Unpremul);

    internal static SKImage CreateImage(int width, int height, byte[] bgra, SKAlphaType alphaType)
    {
        var info = new SKImageInfo(width, height, SKColorType.Bgra8888, alphaType);
        return SKImage.FromPixelCopy(info, bgra)
            ?? throw new InvalidOperationException("Skia could not create an image from the decoded pixels.");
    }
}

/// <summary>
/// Decodes the input files with SkiaSharp. Applies EXIF orientation from the codec origin using the
/// same eight transforms as GDI+ RotateFlip. Preview mode also makes downscaled copies.
/// </summary>
internal static class ImageLoader
{
    internal const long MaximumImagePixels = 40_000_000;
    internal const long MaximumSelectionPixels = 80_000_000;

    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"
    };

    private readonly record struct SourceInfo(string Path, string FileName, int StoredWidth, int StoredHeight, int Orientation);

    internal static List<PixelBuffer> Load(IReadOnlyList<string> candidates, int? previewLongEdge, SKSamplingOptions sampling)
    {
        // Pass 1 checks every file's presence, type, and size before any pixel is decoded, as the Windows code does.
        var infos = new List<SourceInfo>(candidates.Count);
        long totalPixels = 0;
        foreach (var candidate in candidates)
        {
            var path = Path.GetFullPath(candidate);
            if (!File.Exists(path))
                throw new SoftstageException($"The selected image was not found: {candidate}");

            var fileName = Path.GetFileName(path);
            if (!SupportedExtensions.Contains(Path.GetExtension(path)))
                throw new SoftstageException($"'{fileName}' is not a supported image. Choose PNG, JPEG, BMP, GIF, or TIFF files.");

            var info = Inspect(path, fileName);
            var pixels = (long)info.StoredWidth * info.StoredHeight;
            if (pixels > MaximumImagePixels)
                throw new SoftstageException($"'{fileName}' is larger than the 40-megapixel limit.");

            totalPixels += pixels;
            if (totalPixels > MaximumSelectionPixels)
                throw new SoftstageException("The selected images exceed the 80-megapixel total limit.");

            infos.Add(info);
        }

        var images = new List<PixelBuffer>(infos.Count);
        foreach (var info in infos)
            images.Add(Decode(info, previewLongEdge, sampling));
        return images;
    }

    private static SourceInfo Inspect(string path, string fileName)
    {
        using var codec = OpenCodec(path, fileName);
        return new SourceInfo(path, fileName, codec.Info.Width, codec.Info.Height, ToExifOrientation(codec.EncodedOrigin));
    }

    private static PixelBuffer Decode(SourceInfo info, int? previewLongEdge, SKSamplingOptions sampling)
    {
        using var codec = OpenCodec(info.Path, info.FileName);
        var storedInfo = new SKImageInfo(info.StoredWidth, info.StoredHeight, SKColorType.Bgra8888, SKAlphaType.Unpremul);
        var stored = new byte[checked(info.StoredWidth * info.StoredHeight * 4)];

        var handle = GCHandle.Alloc(stored, GCHandleType.Pinned);
        SKCodecResult result;
        try
        {
            result = codec.GetPixels(storedInfo, handle.AddrOfPinnedObject());
        }
        catch (Exception ex) when (ex is not SoftstageException)
        {
            throw DecodeFailure(info.FileName, ex);
        }
        finally
        {
            handle.Free();
        }

        if (result != SKCodecResult.Success)
            throw DecodeFailure(info.FileName, null);

        var oriented = Orient(stored, info.StoredWidth, info.StoredHeight, info.Orientation, out var width, out var height);
        if (previewLongEdge is not { } cap)
            return new PixelBuffer(width, height, oriented);

        var (targetWidth, targetHeight) = PreviewSize(width, height, cap);
        return Thumbnail(oriented, width, height, targetWidth, targetHeight, sampling);
    }

    private static SKCodec OpenCodec(string path, string fileName)
    {
        SKCodec? codec;
        try
        {
            codec = SKCodec.Create(path);
        }
        catch (Exception ex) when (ex is not SoftstageException)
        {
            throw DecodeFailure(fileName, ex);
        }

        return codec ?? throw DecodeFailure(fileName, null);
    }

    private static SoftstageException DecodeFailure(string fileName, Exception? inner) => new(
        $"'{fileName}' could not be decoded as an image. Check the file and try again.", inner);

    /// <summary>Maps the stored pixels to the displayed orientation (EXIF 1 to 8).</summary>
    internal static byte[] Orient(byte[] stored, int width, int height, int orientation, out int outWidth, out int outHeight)
    {
        var swap = orientation is >= 5 and <= 8;
        outWidth = swap ? height : width;
        outHeight = swap ? width : height;
        if (orientation is < 2 or > 8)
            return stored;

        var result = new byte[checked(outWidth * outHeight * 4)];
        for (var y = 0; y < outHeight; y++)
            for (var x = 0; x < outWidth; x++)
            {
                int sourceX, sourceY;
                switch (orientation)
                {
                    case 2: sourceX = width - 1 - x; sourceY = y; break;
                    case 3: sourceX = width - 1 - x; sourceY = height - 1 - y; break;
                    case 4: sourceX = x; sourceY = height - 1 - y; break;
                    case 5: sourceX = y; sourceY = x; break;
                    case 6: sourceX = y; sourceY = height - 1 - x; break;
                    case 7: sourceX = width - 1 - y; sourceY = height - 1 - x; break;
                    default: sourceX = width - 1 - y; sourceY = x; break;
                }
                Buffer.BlockCopy(stored, (sourceY * width + sourceX) * 4, result, (y * outWidth + x) * 4, 4);
            }
        return result;
    }

    internal static int ToExifOrientation(SKEncodedOrigin origin)
    {
        var value = (int)origin;
        return value is >= 1 and <= 8 ? value : 1;
    }

    private static (int Width, int Height) PreviewSize(int width, int height, int cap)
    {
        var scale = Math.Min(1d, cap / (double)Math.Max(width, height));
        return (Math.Max(1, (int)Math.Round(width * scale)), Math.Max(1, (int)Math.Round(height * scale)));
    }

    private static PixelBuffer Thumbnail(byte[] source, int width, int height, int targetWidth, int targetHeight, SKSamplingOptions sampling)
    {
        using var sourceImage = new PixelBuffer(width, height, source).ToImage();
        using var target = new SKBitmap(new SKImageInfo(targetWidth, targetHeight, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(target))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawImage(sourceImage,
                new SKRect(0, 0, width, height),
                new SKRect(0, 0, targetWidth, targetHeight),
                sampling);
        }

        var premultiplied = new byte[targetWidth * targetHeight * 4];
        Marshal.Copy(target.GetPixels(), premultiplied, 0, premultiplied.Length);
        return new PixelBuffer(targetWidth, targetHeight, Unpremultiply(premultiplied));
    }

    private static byte[] Unpremultiply(byte[] premultiplied)
    {
        var result = new byte[premultiplied.Length];
        for (var i = 0; i < premultiplied.Length; i += 4)
        {
            var alpha = premultiplied[i + 3];
            result[i + 3] = alpha;
            if (alpha == 0)
                continue;
            for (var channel = 0; channel < 3; channel++)
                result[i + channel] = (byte)Math.Min(255, (premultiplied[i + channel] * 255 + alpha / 2) / alpha);
        }
        return result;
    }
}

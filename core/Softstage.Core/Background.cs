using System.Runtime.InteropServices;
using SkiaSharp;

namespace Softstage.Core;

internal readonly record struct Rgb(int R, int G, int B)
{
    public static readonly Rgb White = new(255, 255, 255);
    public static readonly Rgb Black = new(0, 0, 0);
}

/// <summary>
/// Background fills. Port of BackdropRenderer's DrawBackground, FillGradient, pattern tiles,
/// dominant-colour sampling, and the integer gradient dither. Output is a BGRA byte buffer (alpha 255).
/// </summary>
internal static class Background
{
    private sealed class Bucket
    {
        public long Count;
        public long Red;
        public long Green;
        public long Blue;
    }

    private readonly record struct Sample(int Key, Rgb Color, long Count);

    internal static byte[] Create(int width, int height, IReadOnlyList<PixelBuffer> images, AppSettings settings)
    {
        var pixels = new byte[checked(width * height * 4)];
        var first = ParseHex(settings.BackgroundColor1Hex);
        switch (settings.BackgroundMode)
        {
            case BackgroundMode.AutoGradient:
            {
                var (start, end) = DominantColors(images, settings.AutoGradientLightenPercent);
                FillGradient(pixels, width, height, start, end);
                break;
            }
            case BackgroundMode.SolidColor:
                FillSolid(pixels, first);
                break;
            case BackgroundMode.CustomGradient:
                FillGradient(pixels, width, height, first, ParseHex(settings.BackgroundColor2Hex));
                break;
            case BackgroundMode.Pattern:
                FillSolid(pixels, first);
                ApplyPattern(pixels, width, height, first, settings.BackgroundPattern);
                break;
            default:
                throw new SoftstageException("The background mode is invalid.");
        }
        return pixels;
    }

    internal static Rgb ParseHex(string hex) => new(
        Convert.ToInt32(hex.Substring(1, 2), 16),
        Convert.ToInt32(hex.Substring(3, 2), 16),
        Convert.ToInt32(hex.Substring(5, 2), 16));

    private static void FillSolid(byte[] pixels, Rgb color)
    {
        for (var i = 0; i < pixels.Length; i += 4)
        {
            pixels[i] = (byte)color.B;
            pixels[i + 1] = (byte)color.G;
            pixels[i + 2] = (byte)color.R;
            pixels[i + 3] = 255;
        }
    }

    private static void FillGradient(byte[] pixels, int width, int height, Rgb first, Rgb second)
    {
        if (first == second)
        {
            FillSolid(pixels, first);
            return;
        }

        var lastX = width - 1;
        var denominator = Math.Max(1, lastX);
        for (var y = 0; y < height; y++)
        {
            var row = y * width * 4;
            for (var x = 0; x < width; x++)
            {
                var pixel = row + x * 4;
                if (x == 0 || x == lastX)
                {
                    var endpoint = x == 0 ? first : second;
                    pixels[pixel] = (byte)endpoint.B;
                    pixels[pixel + 1] = (byte)endpoint.G;
                    pixels[pixel + 2] = (byte)endpoint.R;
                }
                else
                {
                    var amount = x / (float)denominator;
                    var noise = GradientNoise(x, y);
                    pixels[pixel] = DitheredChannel(first.B, second.B, amount, noise);
                    pixels[pixel + 1] = DitheredChannel(first.G, second.G, amount, noise);
                    pixels[pixel + 2] = DitheredChannel(first.R, second.R, amount, noise);
                }
                pixels[pixel + 3] = 255;
            }
        }
    }

    private static byte DitheredChannel(int first, int second, float amount, uint noise)
    {
        var value = first + (second - first) * (double)amount;
        var lower = Math.Floor(value);
        var fraction = value - lower;
        var threshold = (noise >> 8) / 16777216d;
        return (byte)Math.Clamp((int)(lower + (threshold < fraction ? 1 : 0)), 0, 255);
    }

    private static uint GradientNoise(int x, int y)
    {
        unchecked
        {
            var value = (uint)x * 0x9E3779B9u ^ (uint)y * 0x85EBCA6Bu ^ 0xC2B2AE35u;
            value ^= value >> 16;
            value *= 0x7FEB352Du;
            value ^= value >> 15;
            value *= 0x846CA68Bu;
            return value ^ (value >> 16);
        }
    }

    private static void ApplyPattern(byte[] pixels, int width, int height, Rgb baseColor, BackgroundPattern pattern)
    {
        switch (pattern)
        {
            case BackgroundPattern.SoftGrain:
                Composite(pixels, width, height, GrainTile(baseColor), 128, 128, premultiplied: false);
                break;
            case BackgroundPattern.Dots:
                var dots = DotTile(baseColor, width, height, out var tileSize);
                Composite(pixels, width, height, dots, tileSize, tileSize, premultiplied: true);
                break;
            default:
                throw new SoftstageException("The background pattern is invalid.");
        }
    }

    /// <summary>Source-over a repeating tile (origin at the canvas corner) onto an opaque buffer.</summary>
    private static void Composite(byte[] pixels, int width, int height, byte[] tile, int tileWidth, int tileHeight, bool premultiplied)
    {
        for (var y = 0; y < height; y++)
        {
            var tileRow = y % tileHeight * tileWidth;
            for (var x = 0; x < width; x++)
            {
                var source = (tileRow + x % tileWidth) * 4;
                var alpha = tile[source + 3];
                if (alpha == 0)
                    continue;

                var target = (y * width + x) * 4;
                for (var channel = 0; channel < 3; channel++)
                {
                    int src = tile[source + channel];
                    int dst = pixels[target + channel];
                    var value = premultiplied
                        ? src + (dst * (255 - alpha) + 127) / 255
                        : (src * alpha + dst * (255 - alpha) + 127) / 255;
                    pixels[target + channel] = (byte)Math.Clamp(value, 0, 255);
                }
            }
        }
    }

    /// <summary>128 x 128 grain tile from the seeded Random(0x5EED), same draw order as CreateGrainTile.</summary>
    private static byte[] GrainTile(Rgb baseColor)
    {
        const int size = 128;
        var tile = new byte[size * size * 4];
        var random = new Random(0x5EED);
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var offset = random.Next(-64, 65);
                var pixel = (y * size + x) * 4;
                tile[pixel] = Shade(baseColor.B, offset);
                tile[pixel + 1] = Shade(baseColor.G, offset);
                tile[pixel + 2] = Shade(baseColor.R, offset);
                tile[pixel + 3] = (byte)random.Next(7, 17);
            }
        return tile;
    }

    private static byte Shade(int component, int offset) => (byte)Math.Clamp(
        offset >= 0 ? component + (255 - component) * offset / 64 : component + component * offset / 64,
        0, 255);

    /// <summary>
    /// Dot tile, premultiplied BGRA. Circles are drawn with Skia anti-aliasing, so coverage at the
    /// edges differs slightly from GDI+.
    /// </summary>
    private static byte[] DotTile(Rgb baseColor, int canvasWidth, int canvasHeight, out int tileSize)
    {
        var longEdge = Math.Max(canvasWidth, canvasHeight);
        var spacing = Math.Clamp(longEdge / 50, 12, 72);
        tileSize = spacing * 4;
        var radius = Math.Clamp(longEdge / 1500f, 0.8f, 2.5f);
        var lightBackground = Brightness(baseColor) >= 0.5f;
        var contrast = lightBackground ? 0 : 255;
        var alpha = lightBackground ? 36 : 18;

        using var bitmap = new SKBitmap(new SKImageInfo(tileSize, tileSize, SKColorType.Bgra8888, SKAlphaType.Premul));
        using (var canvas = new SKCanvas(bitmap))
        using (var paint = new SKPaint
        {
            IsAntialias = true,
            Color = new SKColor((byte)contrast, (byte)contrast, (byte)contrast, (byte)alpha)
        })
        {
            canvas.Clear(SKColors.Transparent);
            for (var y = spacing / 2f; y < tileSize; y += spacing)
                for (var x = spacing / 2f; x < tileSize; x += spacing)
                    canvas.DrawCircle(x, y, radius, paint);
        }

        var bytes = new byte[tileSize * tileSize * 4];
        Marshal.Copy(bitmap.GetPixels(), bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>Dominant-colour sampling: 24 x 24 grid per image, quantised buckets, two-cluster refinement.</summary>
    internal static (Rgb First, Rgb Second) DominantColors(IReadOnlyList<PixelBuffer> images, int lightenPercent)
    {
        var buckets = new Dictionary<int, Bucket>();
        foreach (var image in images)
        {
            const int samplesPerAxis = 24;
            for (var y = 0; y < samplesPerAxis; y++)
                for (var x = 0; x < samplesPerAxis; x++)
                {
                    var sampleX = Math.Min(image.Width - 1, x * image.Width / samplesPerAxis);
                    var sampleY = Math.Min(image.Height - 1, y * image.Height / samplesPerAxis);
                    var index = (sampleY * image.Width + sampleX) * 4;
                    var b = image.Bgra[index];
                    var g = image.Bgra[index + 1];
                    var r = image.Bgra[index + 2];
                    var a = image.Bgra[index + 3];
                    if (a < 24)
                        continue;

                    var key = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
                    if (!buckets.TryGetValue(key, out var bucket))
                        buckets[key] = bucket = new Bucket();
                    bucket.Count++;
                    bucket.Red += r;
                    bucket.Green += g;
                    bucket.Blue += b;
                }
        }

        var dominant = buckets.OrderBy(pair => pair.Key)
            .Select(pair => new Sample(pair.Key, Average(pair.Value), pair.Value.Count))
            .ToArray();
        if (dominant.Length == 0)
            return (LightenAutomaticColor(new Rgb(91, 104, 122), lightenPercent),
                LightenAutomaticColor(new Rgb(145, 153, 166), lightenPercent));

        var colored = dominant.Where(item => Saturation(item.Color) >= 0.2f && Brightness(item.Color) >= 0.08f).ToArray();
        var mostSupportedColor = colored.OrderByDescending(item => item.Count).ThenBy(item => item.Key).FirstOrDefault();
        var total = dominant.Sum(item => item.Count);
        var candidates = colored.Length > 0 && mostSupportedColor.Count >= total * 0.05
            ? colored
            : dominant;
        var firstSeed = candidates.OrderByDescending(item => item.Count).ThenBy(item => item.Key).First();
        var secondSeed = candidates.Where(item => item.Key != firstSeed.Key)
            .OrderByDescending(item => item.Count * ColorDistanceSquared(firstSeed.Color, item.Color))
            .ThenBy(item => item.Key)
            .FirstOrDefault();

        var first = firstSeed.Color;
        Rgb? second = null;
        if (secondSeed.Count != 0)
        {
            var refined = RefineColorClusters(candidates, firstSeed.Color, secondSeed.Color);
            first = refined.First;
            second = refined.Second;
        }

        if (second is null || ColorDistance(first, second.Value) < 50)
            second = Brightness(first) < 0.7f
                ? Blend(first, Rgb.White, 0.32f)
                : Blend(first, Rgb.Black, 0.25f);

        return (LightenAutomaticColor(first, lightenPercent), LightenAutomaticColor(second.Value, lightenPercent));
    }

    private static (Rgb First, Rgb Second) RefineColorClusters(Sample[] samples, Rgb first, Rgb second)
    {
        for (var iteration = 0; iteration < 6; iteration++)
        {
            var firstCluster = new Bucket();
            var secondCluster = new Bucket();
            foreach (var sample in samples)
            {
                var cluster = ColorDistanceSquared(sample.Color, first) <= ColorDistanceSquared(sample.Color, second)
                    ? firstCluster
                    : secondCluster;
                cluster.Count += sample.Count;
                cluster.Red += sample.Count * sample.Color.R;
                cluster.Green += sample.Count * sample.Color.G;
                cluster.Blue += sample.Count * sample.Color.B;
            }

            var nextFirst = firstCluster.Count == 0 ? first : Average(firstCluster);
            var nextSecond = secondCluster.Count == 0 ? second : Average(secondCluster);
            if (nextFirst == first && nextSecond == second)
                break;
            first = nextFirst;
            second = nextSecond;
        }
        return (first, second);
    }

    private static Rgb LightenAutomaticColor(Rgb color, int percent) => Blend(color, Rgb.White, percent / 100f);

    private static Rgb Average(Bucket bucket) => new(
        (int)(bucket.Red / bucket.Count),
        (int)(bucket.Green / bucket.Count),
        (int)(bucket.Blue / bucket.Count));

    private static Rgb Blend(Rgb from, Rgb to, float amount) => new(
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));

    private static double ColorDistance(Rgb first, Rgb second) => Math.Sqrt(ColorDistanceSquared(first, second));

    private static double ColorDistanceSquared(Rgb first, Rgb second)
    {
        var red = first.R - second.R;
        var green = first.G - second.G;
        var blue = first.B - second.B;
        return red * red + green * green + blue * blue;
    }

    /// <summary>System.Drawing.Color.GetBrightness, checked bit-for-bit against .NET 10 on 300k colours.</summary>
    internal static float Brightness(Rgb color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        return (float)((max + min) / 2);
    }

    /// <summary>System.Drawing.Color.GetSaturation, checked bit-for-bit against .NET 10 on 300k colours.</summary>
    internal static float Saturation(Rgb color)
    {
        double r = color.R / 255.0, g = color.G / 255.0, b = color.B / 255.0;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        if (max == min)
            return 0f;
        return (float)((max + min) / 2 <= 0.5
            ? (max - min) / (max + min)
            : (max - min) / (2.0 - max - min));
    }
}

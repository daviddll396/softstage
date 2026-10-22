using System.Runtime.InteropServices;
using SkiaSharp;

namespace Softstage.Core;

/// <summary>
/// Soft drop shadow. Port of BackdropRenderer.DrawShadow and BlurShadowMask. The mask is built at a
/// reduced long edge, blurred in managed code, then drawn back at full size with the image sampler.
/// </summary>
internal static class ShadowRenderer
{
    /// <summary>Blur padding in canvas pixels around each image. The shadow is zero outside it.</summary>
    internal static int Padding(int canvasWidth)
    {
        var blur = Math.Clamp(canvasWidth * 0.022f, 12, 80);
        var sigma = blur * 0.65f;
        return (int)Math.Ceiling(sigma * 3f);
    }

    internal static void Draw(SKCanvas canvas, RectF bounds, double strength, int canvasWidth, int maskLongEdge, SKSamplingOptions sampling)
    {
        if (strength <= 0)
            return;

        const float offset = 0f;
        var blur = Math.Clamp(canvasWidth * 0.022f, 12, 80);
        var sigma = blur * 0.65f;
        var padding = (int)Math.Ceiling(sigma * 3f + offset);
        var scale = Math.Min(1f, maskLongEdge / Math.Max(bounds.Width, bounds.Height));
        var width = Math.Max(1, (int)Math.Ceiling((bounds.Width + padding * 2) * scale));
        var height = Math.Max(1, (int)Math.Ceiling((bounds.Height + padding * 2) * scale));

        var alpha = new byte[width * height];
        var fillAlpha = (byte)Math.Clamp((int)Math.Round(strength * 120), 0, 255);
        FillRectangle(alpha, width, height,
            padding * scale, (padding + offset) * scale, bounds.Width * scale, bounds.Height * scale, fillAlpha);

        var blurred = BlurAlpha(alpha, width, height, sigma * scale);

        var pixels = new byte[width * height * 4];
        for (var i = 0; i < blurred.Length; i++)
            pixels[i * 4 + 3] = blurred[i];

        using var mask = PixelBuffer.CreateImage(width, height, pixels, SKAlphaType.Premul);
        var destination = new SKRect(
            bounds.Left - padding,
            bounds.Top - padding,
            bounds.Right + padding,
            bounds.Bottom + padding);
        canvas.DrawImage(mask, new SKRect(0, 0, width, height), destination, sampling);
    }

    /// <summary>Solid fill of a rectangle into an alpha buffer. Pixel centres decide coverage (no anti-aliasing).</summary>
    private static void FillRectangle(byte[] alpha, int width, int height, float x, float y, float rectWidth, float rectHeight, byte value)
    {
        for (var py = 0; py < height; py++)
        {
            var centreY = py + 0.5f;
            if (centreY < y || centreY >= y + rectHeight)
                continue;
            for (var px = 0; px < width; px++)
            {
                var centreX = px + 0.5f;
                if (centreX < x || centreX >= x + rectWidth)
                    continue;
                alpha[py * width + px] = value;
            }
        }
    }

    /// <summary>Separable Gaussian blur of an alpha buffer, same kernel and rounding as BlurShadowMask.</summary>
    private static byte[] BlurAlpha(byte[] source, int width, int height, float sigma)
    {
        if (sigma < 0.5f)
            return source;

        var radius = (int)Math.Ceiling(sigma * 3);
        var kernel = Enumerable.Range(-radius, radius * 2 + 1)
            .Select(offset => (float)Math.Exp(-(offset * offset) / (2 * sigma * sigma)))
            .ToArray();
        var weight = kernel.Sum();
        for (var i = 0; i < kernel.Length; i++)
            kernel[i] /= weight;

        var horizontal = new byte[source.Length];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var value = 0f;
                for (var k = -radius; k <= radius; k++)
                {
                    var sampleX = x + k;
                    if ((uint)sampleX < (uint)width)
                        value += source[y * width + sampleX] * kernel[k + radius];
                }
                horizontal[y * width + x] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
            }

        var blurred = new byte[source.Length];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var value = 0f;
                for (var k = -radius; k <= radius; k++)
                {
                    var sampleY = y + k;
                    if ((uint)sampleY < (uint)height)
                        value += horizontal[sampleY * width + x] * kernel[k + radius];
                }
                blurred[y * width + x] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
            }

        return blurred;
    }
}

using SkiaSharp;
using Softstage.Core;

namespace Softstage.ParityCheck;

/// <summary>
/// Checks the EXIF orientation mapping. The stored image has four solid quadrants: TL red, TR green, BL blue,
/// BR yellow. For each EXIF tag 1-8 the expected display layout is written as a 4-letter table (TL, TR, BL, BR)
/// from the EXIF transform definitions, not from the code under test.
/// </summary>
internal static class ExifChecks
{
    private const int StoredWidth = 64;
    private const int StoredHeight = 32;
    private const int Tolerance = 24;

    private static readonly Dictionary<int, string> ExpectedLayouts = new()
    {
        [1] = "RGBY",
        [2] = "GRYB",
        [3] = "YBGR",
        [4] = "BYRG",
        [5] = "RBGY",
        [6] = "BRYG",
        [7] = "YGBR",
        [8] = "GYRB"
    };

    private static readonly Dictionary<char, Rgb> Colors = new()
    {
        ['R'] = new Rgb(255, 0, 0),
        ['G'] = new Rgb(0, 255, 0),
        ['B'] = new Rgb(0, 0, 255),
        ['Y'] = new Rgb(255, 255, 0)
    };

    public static bool Run(string outputDirectory)
    {
        Console.WriteLine("== EXIF orientation (JPEG with orientation tags 1-8) ==");
        var directory = Path.Combine(outputDirectory, "exif");
        Directory.CreateDirectory(directory);
        var jpeg = EncodeQuadrantJpeg();

        var allOk = true;
        for (var orientation = 1; orientation <= 8; orientation++)
        {
            var path = Path.Combine(directory, $"orientation-{orientation}.jpg");
            File.WriteAllBytes(path, WithOrientation(jpeg, orientation));

            var loaded = ImageLoader.Load(new[] { path }, null, Compositor.DefaultSampling)[0];
            var swapped = orientation is >= 5 and <= 8;
            var expectedWidth = swapped ? StoredHeight : StoredWidth;
            var expectedHeight = swapped ? StoredWidth : StoredHeight;
            var dimensionsOk = loaded.Width == expectedWidth && loaded.Height == expectedHeight;

            var expected = ExpectedLayouts[orientation];
            var samples = new[]
            {
                (X: loaded.Width / 4, Y: loaded.Height / 4),
                (X: loaded.Width * 3 / 4, Y: loaded.Height / 4),
                (X: loaded.Width / 4, Y: loaded.Height * 3 / 4),
                (X: loaded.Width * 3 / 4, Y: loaded.Height * 3 / 4)
            };
            var actual = new string(samples.Select(s => NearestLetter(Pixel(loaded, s.X, s.Y))).ToArray());
            var layoutOk = actual == expected;

            var ok = dimensionsOk && layoutOk;
            allOk &= ok;
            Console.WriteLine($"  tag {orientation}: {loaded.Width}x{loaded.Height} (expected {expectedWidth}x{expectedHeight}), TL/TR/BL/BR = {actual} (expected {expected})  {(ok ? "ok" : "MISMATCH")}");
        }

        Console.WriteLine($"[{(allOk ? "PASS" : "FAIL")}] EXIF orientation tags 1-8 map to the EXIF transforms");
        return allOk;
    }

    private static byte[] EncodeQuadrantJpeg()
    {
        using var bitmap = new SKBitmap(new SKImageInfo(StoredWidth, StoredHeight, SKColorType.Bgra8888, SKAlphaType.Opaque));
        for (var y = 0; y < StoredHeight; y++)
            for (var x = 0; x < StoredWidth; x++)
            {
                var letter = (x < StoredWidth / 2, y < StoredHeight / 2) switch
                {
                    (true, true) => 'R',
                    (false, true) => 'G',
                    (true, false) => 'B',
                    _ => 'Y'
                };
                var color = Colors[letter];
                bitmap.SetPixel(x, y, new SKColor((byte)color.R, (byte)color.G, (byte)color.B));
            }

        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, 100);
        return data.ToArray();
    }

    /// <summary>Inserts an APP1 EXIF segment that holds only the orientation tag (0x0112), right after SOI.</summary>
    private static byte[] WithOrientation(byte[] jpeg, int orientation)
    {
        if (jpeg[0] != 0xFF || jpeg[1] != 0xD8)
            throw new InvalidOperationException("The test JPEG does not start with SOI.");

        byte[] segment =
        {
            0xFF, 0xE1, 0x00, 0x22,
            (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0x00, 0x00,
            (byte)'I', (byte)'I', 0x2A, 0x00, 0x08, 0x00, 0x00, 0x00,
            0x01, 0x00,
            0x12, 0x01, 0x03, 0x00, 0x01, 0x00, 0x00, 0x00, (byte)orientation, 0x00, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x00
        };

        var result = new byte[jpeg.Length + segment.Length];
        Buffer.BlockCopy(jpeg, 0, result, 0, 2);
        Buffer.BlockCopy(segment, 0, result, 2, segment.Length);
        Buffer.BlockCopy(jpeg, 2, result, 2 + segment.Length, jpeg.Length - 2);
        return result;
    }

    private static Rgb Pixel(PixelBuffer image, int x, int y)
    {
        var p = (y * image.Width + x) * 4;
        return new Rgb(image.Bgra[p + 2], image.Bgra[p + 1], image.Bgra[p]);
    }

    /// <summary>Maps a sampled colour to the nearest quadrant letter, or '?' outside the tolerance.</summary>
    private static char NearestLetter(Rgb sample)
    {
        foreach (var (letter, color) in Colors)
        {
            if (Math.Abs(sample.R - color.R) <= Tolerance && Math.Abs(sample.G - color.G) <= Tolerance && Math.Abs(sample.B - color.B) <= Tolerance)
                return letter;
        }
        return '?';
    }
}

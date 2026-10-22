using SkiaSharp;
using Softstage.Core;

namespace Softstage.ParityCheck;

/// <summary>Error statistics for one region. Channels are R, G, B. The pooled histogram covers all three channels.</summary>
internal sealed class RegionStats
{
    public RegionStats(string name)
    {
        Name = name;
    }

    public string Name { get; }
    public long Count { get; private set; }
    public long Over8 { get; private set; }
    public long Over32 { get; private set; }
    private readonly long[] _sumAbs = new long[3];
    private readonly long[] _sumSigned = new long[3];
    private readonly long[] _histogram = new long[256];

    public void Add(int dr, int dg, int db)
    {
        Count++;
        AddChannel(0, dr);
        AddChannel(1, dg);
        AddChannel(2, db);
        var max = Math.Max(Math.Abs(dr), Math.Max(Math.Abs(dg), Math.Abs(db)));
        if (max > 8)
            Over8++;
        if (max > 32)
            Over32++;
    }

    private void AddChannel(int channel, int difference)
    {
        _sumAbs[channel] += Math.Abs(difference);
        _sumSigned[channel] += difference;
        _histogram[Math.Abs(difference)]++;
    }

    public double Mae(int channel) => Count == 0 ? double.NaN : _sumAbs[channel] / (double)Count;

    public double MeanSigned(int channel) => Count == 0 ? double.NaN : _sumSigned[channel] / (double)Count;

    /// <summary>Mean of the three per-channel MAE values.</summary>
    public double MeanMae => Count == 0 ? double.NaN : (Mae(0) + Mae(1) + Mae(2)) / 3;

    /// <summary>Largest per-channel MAE.</summary>
    public double WorstMae => Count == 0 ? double.NaN : Math.Max(Mae(0), Math.Max(Mae(1), Mae(2)));

    /// <summary>Smallest value v such that at least 99% of pooled channel differences are at most v.</summary>
    public int P99()
    {
        var total = Count * 3;
        var target = (long)Math.Ceiling(total * 0.99);
        long cumulative = 0;
        for (var value = 0; value < 256; value++)
        {
            cumulative += _histogram[value];
            if (cumulative >= target)
                return value;
        }
        return 255;
    }

    public double PercentOver8 => Count == 0 ? 0 : 100.0 * Over8 / Count;

    public double PercentOver32 => Count == 0 ? 0 : 100.0 * Over32 / Count;
}

internal sealed class ComparisonReport
{
    public required RegionStats Whole { get; init; }
    public required RegionStats AllImages { get; init; }
    public required RegionStats Ring { get; init; }
    public required RegionStats Background { get; init; }
    public required List<RegionStats> PerImage { get; init; }
    public required byte[] MaxChannelDifference { get; init; }

    public IEnumerable<RegionStats> All =>
        new[] { Whole, AllImages }.Concat(PerImage).Concat(new[] { Ring, Background });
}

internal static class Metrics
{
    /// <summary>
    /// Compares two BGRA buffers of the same size. Regions: whole canvas; union of image rectangles and each
    /// rectangle (pixel centre inside); shadow ring (inside the blur padding of an image, outside all images);
    /// background only (outside every padded rectangle, so no shadow).
    /// </summary>
    internal static ComparisonReport Compare(byte[] output, byte[] reference, int width, int height, CanvasGeometry geometry, int padding)
    {
        var bounds = geometry.ImageBounds;
        var whole = new RegionStats("whole canvas");
        var allImages = new RegionStats("all image rectangles");
        var ring = new RegionStats("shadow ring (padding, outside images)");
        var background = new RegionStats("background only (outside padding)");
        var perImage = Enumerable.Range(0, bounds.Count)
            .Select(i => new RegionStats($"image {i + 1} rectangle"))
            .ToList();
        var maxDifference = new byte[width * height];

        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var p = (y * width + x) * 4;
                var db = output[p] - reference[p];
                var dg = output[p + 1] - reference[p + 1];
                var dr = output[p + 2] - reference[p + 2];
                whole.Add(dr, dg, db);
                maxDifference[y * width + x] = (byte)Math.Min(255, Math.Max(Math.Abs(dr), Math.Max(Math.Abs(dg), Math.Abs(db))));

                var centreX = x + 0.5f;
                var centreY = y + 0.5f;
                var hit = -1;
                for (var i = 0; i < bounds.Count; i++)
                {
                    var b = bounds[i];
                    if (centreX >= b.Left && centreX < b.Right && centreY >= b.Top && centreY < b.Bottom)
                    {
                        hit = i;
                        break;
                    }
                }

                if (hit >= 0)
                {
                    allImages.Add(dr, dg, db);
                    perImage[hit].Add(dr, dg, db);
                    continue;
                }

                var inPadding = false;
                foreach (var b in bounds)
                {
                    if (centreX >= b.Left - padding && centreX < b.Right + padding &&
                        centreY >= b.Top - padding && centreY < b.Bottom + padding)
                    {
                        inPadding = true;
                        break;
                    }
                }

                if (inPadding)
                    ring.Add(dr, dg, db);
                else
                    background.Add(dr, dg, db);
            }

        return new ComparisonReport
        {
            Whole = whole,
            AllImages = allImages,
            Ring = ring,
            Background = background,
            PerImage = perImage,
            MaxChannelDifference = maxDifference
        };
    }

    internal static void Print(RegionStats s)
    {
        Console.WriteLine(
            $"  {s.Name,-40} px={s.Count,10}  MAE R/G/B={s.Mae(0):F3}/{s.Mae(1):F3}/{s.Mae(2):F3}  " +
            $"bias R/G/B={s.MeanSigned(0):+0.000;-0.000;0.000}/{s.MeanSigned(1):+0.000;-0.000;0.000}/{s.MeanSigned(2):+0.000;-0.000;0.000}  " +
            $"p99={s.P99(),3}  >8={s.PercentOver8,7:F3}%  >32={s.PercentOver32,8:F4}%");
    }
}

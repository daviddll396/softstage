using SkiaSharp;

namespace Softstage.Core;

internal readonly record struct ImageSize(int Width, int Height);

/// <summary>Float rectangle with the same semantics as System.Drawing.RectangleF.</summary>
internal readonly record struct RectF(float Left, float Top, float Width, float Height)
{
    public float Right => Left + Width;
    public float Bottom => Top + Height;

    public SKRect ToSk() => new(Left, Top, Right, Bottom);
}

internal sealed record CanvasGeometry(int Width, int Height, IReadOnlyList<RectF> ImageBounds);

/// <summary>Canvas size, padding, and image placement. Port of BackdropRenderer's geometry code.</summary>
internal static class CanvasLayout
{
    private const double MinimumAutoCompositionRatio = 0.5;
    private const double MaximumAutoCompositionRatio = 3;

    internal static CanvasGeometry Compute(IReadOnlyList<ImageSize> sizes, AppSettings settings)
    {
        var (width, height) = CanvasSize(sizes, settings);
        return new CanvasGeometry(width, height, ImageBounds(sizes, settings, width, height));
    }

    internal static (int Width, int Height) CanvasSize(IReadOnlyList<ImageSize> sizes, AppSettings settings)
    {
        var layout = ResolveLayout(sizes, settings.Layout);
        var ratio = settings.CanvasRatio switch
        {
            CanvasRatio.Wide16x9 => 16d / 9,
            CanvasRatio.Square1x1 => 1,
            CanvasRatio.Portrait4x5 => 4d / 5,
            _ when sizes.Count == 1 => sizes[0].Width / (double)sizes[0].Height,
            _ when layout == CompositionLayout.Row => NaturalRowRatio(sizes),
            _ => NaturalGridRatio(sizes)
        };
        var longEdge = settings.OutputWidth;
        return ratio >= 1
            ? (longEdge, Math.Max(1, (int)Math.Round(longEdge / ratio)))
            : (Math.Max(1, (int)Math.Round(longEdge * ratio)), longEdge);
    }

    internal static CompositionLayout ResolveLayout(IReadOnlyList<ImageSize> sizes, CompositionLayout layout) =>
        sizes.Count == 1 ? CompositionLayout.Row : layout == CompositionLayout.Auto
            ? sizes.Count <= 4 && sizes.All(size => size.Height > size.Width) ? CompositionLayout.Row : CompositionLayout.Grid
            : layout;

    private static double NaturalRowRatio(IReadOnlyList<ImageSize> sizes) => Math.Clamp(
        sizes.Sum(size => size.Width / (double)size.Height) + 0.04 * (sizes.Count - 1),
        MinimumAutoCompositionRatio,
        MaximumAutoCompositionRatio);

    private static double NaturalGridRatio(IReadOnlyList<ImageSize> sizes)
    {
        var columns = sizes.Count <= 4 ? 2 : 3;
        var rows = (int)Math.Ceiling(sizes.Count / (double)columns);
        var averageImageRatio = sizes.Average(size => size.Width / (double)size.Height);
        return Math.Clamp(columns * averageImageRatio / rows, MinimumAutoCompositionRatio, MaximumAutoCompositionRatio);
    }

    private static RectF[] ImageBounds(IReadOnlyList<ImageSize> sizes, AppSettings settings, int width, int height)
    {
        var paddingX = width * settings.PaddingPercent / 100f;
        var paddingY = height * settings.PaddingPercent / 100f;
        var content = new RectF(paddingX, paddingY, width - 2 * paddingX, height - 2 * paddingY);

        if (sizes.Count == 1)
        {
            var box = Fit(sizes[0], content.Width, content.Height);
            return [Center(box, content)];
        }

        if (ResolveLayout(sizes, settings.Layout) == CompositionLayout.Row)
        {
            var totalRatio = sizes.Sum(size => size.Width / (float)size.Height);
            const float gapRatio = 0.04f;
            var imageHeight = Math.Min(content.Height, content.Width / (totalRatio + gapRatio * (sizes.Count - 1)));
            var gap = imageHeight * gapRatio;
            var totalWidth = totalRatio * imageHeight + gap * (sizes.Count - 1);
            var left = content.Left + (content.Width - totalWidth) / 2;
            var bounds = new RectF[sizes.Count];
            for (var i = 0; i < sizes.Count; i++)
            {
                var size = sizes[i];
                var imageWidth = size.Width / (float)size.Height * imageHeight;
                bounds[i] = new RectF(left, content.Top + (content.Height - imageHeight) / 2, imageWidth, imageHeight);
                left += imageWidth + gap;
            }
            return bounds;
        }

        var columns = sizes.Count <= 4 ? 2 : 3;
        var rows = (int)Math.Ceiling(sizes.Count / (double)columns);
        var gapX = content.Width * 0.035f;
        var gapY = content.Height * 0.05f;
        var cellWidth = (content.Width - gapX * (columns - 1)) / columns;
        var cellHeight = (content.Height - gapY * (rows - 1)) / rows;
        var result = new RectF[sizes.Count];
        for (var i = 0; i < sizes.Count; i++)
        {
            var row = i / columns;
            var rowItemCount = Math.Min(columns, sizes.Count - row * columns);
            var rowWidth = rowItemCount * cellWidth + (rowItemCount - 1) * gapX;
            var cell = new RectF(
                content.Left + (content.Width - rowWidth) / 2 + (i % columns) * (cellWidth + gapX),
                content.Top + row * (cellHeight + gapY),
                cellWidth,
                cellHeight);
            result[i] = Center(Fit(sizes[i], cell.Width, cell.Height), cell);
        }
        return result;
    }

    private static RectF Fit(ImageSize image, float width, float height)
    {
        var scale = Math.Min(width / image.Width, height / image.Height);
        return new RectF(0, 0, image.Width * scale, image.Height * scale);
    }

    private static RectF Center(RectF size, RectF bounds) => new(
        bounds.Left + (bounds.Width - size.Width) / 2,
        bounds.Top + (bounds.Height - size.Height) / 2,
        size.Width,
        size.Height);

    /// <summary>Rounded-rectangle path with the same arc layout as BackdropControlPaint.RoundedRectangle.</summary>
    internal static SKPath RoundedPath(RectF bounds, float radius)
    {
        var path = new SKPath();
        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 0)
        {
            path.AddRect(bounds.ToSk());
            return path;
        }

        var left = bounds.Left;
        var top = bounds.Top;
        var right = bounds.Right;
        var bottom = bounds.Bottom;
        path.ArcTo(new SKRect(left, top, left + diameter, top + diameter), 180, 90, true);
        path.ArcTo(new SKRect(right - diameter, top, right, top + diameter), 270, 90, false);
        path.ArcTo(new SKRect(right - diameter, bottom - diameter, right, bottom), 0, 90, false);
        path.ArcTo(new SKRect(left, bottom - diameter, left + diameter, bottom), 90, 90, false);
        path.Close();
        return path;
    }
}

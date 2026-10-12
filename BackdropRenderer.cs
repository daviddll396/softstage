using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace Backdrop;

internal static class BackdropRenderer
{
    public const int MaximumImages = 9;
    public const int MaximumPreviewLongEdge = 1000;
    private const int MaximumPreviewShadowMaskLongEdge = 256;
    private const long MaximumImagePixels = 40_000_000;
    private const long MaximumSelectionPixels = 80_000_000;
    private const double MinimumAutoCompositionRatio = 0.5;
    private const double MaximumAutoCompositionRatio = 3;
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"
    };
    private static readonly Lazy<Bitmap[]> SampleArtwork = new(LoadSampleArtwork);

    public static bool IsSupportedImagePath(string path) => SupportedExtensions.Contains(Path.GetExtension(path));

    private sealed record ImageInfo(string Path, int Orientation);
    private sealed record LoadedImage(string Path, Bitmap Bitmap);
    internal sealed class PreviewImageCache : IDisposable
    {
        private sealed record CachedImage(long Length, DateTime LastWriteUtc, long PixelCount, Bitmap Bitmap);

        private readonly Dictionary<string, CachedImage> _images = new(StringComparer.OrdinalIgnoreCase);
        private bool _disposed;

        internal int Count => _images.Count;

        internal IReadOnlyList<Bitmap> Load(IReadOnlyList<string> paths, CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var selected = paths.Select(Path.GetFullPath).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var path in _images.Keys.Where(path => !selected.Contains(path)).ToArray())
            {
                _images[path].Bitmap.Dispose();
                _images.Remove(path);
            }

            var result = new Bitmap[paths.Count];
            long totalPixels = 0;
            for (var i = 0; i < paths.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var path = Path.GetFullPath(paths[i]);
                if (!File.Exists(path))
                    throw new FileNotFoundException($"The selected image was not found: {paths[i]}", path);
                if (!SupportedExtensions.Contains(Path.GetExtension(path)))
                    throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a supported image. Choose PNG, JPEG, BMP, GIF, or TIFF files.");

                var file = new FileInfo(path);
                var length = file.Length;
                var lastWriteUtc = file.LastWriteTimeUtc;
                if (!_images.TryGetValue(path, out var image) || image.Length != length || image.LastWriteUtc != lastWriteUtc)
                {
                    image?.Bitmap.Dispose();
                    _images.Remove(path);
                    image = LoadPreviewImage(path, length, lastWriteUtc, cancellationToken);
                    _images.Add(path, image);
                }

                totalPixels += image.PixelCount;
                if (totalPixels > MaximumSelectionPixels)
                    throw new InvalidDataException("The selected images exceed the 80-megapixel total limit.");
                result[i] = image.Bitmap;
            }
            return result;
        }

        private static CachedImage LoadPreviewImage(string path, long length, DateTime lastWriteUtc, CancellationToken cancellationToken)
        {
            Bitmap? thumbnail = null;
            long pixelCount;
            try
            {
                using (var source = Image.FromFile(path))
                {
                    pixelCount = (long)source.Width * source.Height;
                    if (pixelCount > MaximumImagePixels)
                        throw new InvalidDataException($"'{Path.GetFileName(path)}' is larger than the 40-megapixel limit.");

                    cancellationToken.ThrowIfCancellationRequested();
                    var previewSize = PreviewSize(source.Size);
                    thumbnail = new Bitmap(previewSize.Width, previewSize.Height, PixelFormat.Format32bppPArgb);
                    using (var graphics = Graphics.FromImage(thumbnail))
                    {
                        graphics.CompositingMode = CompositingMode.SourceCopy;
                        graphics.CompositingQuality = CompositingQuality.HighSpeed;
                        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        graphics.DrawImage(source, new Rectangle(Point.Empty, previewSize));
                    }
                    ApplyOrientation(thumbnail, ReadOrientation(source));
                }

                var file = new FileInfo(path);
                file.Refresh();
                if (!file.Exists || file.Length != length || file.LastWriteTimeUtc != lastWriteUtc)
                    throw new IOException($"'{Path.GetFileName(path)}' changed while the preview was loading.");

                return new CachedImage(length, lastWriteUtc, pixelCount, thumbnail);
            }
            catch (InvalidDataException)
            {
                thumbnail?.Dispose();
                throw;
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or ExternalException or IOException or UnauthorizedAccessException)
            {
                thumbnail?.Dispose();
                throw new InvalidDataException($"'{Path.GetFileName(path)}' could not be decoded as an image. Check the file and try again.", ex);
            }
        }

        public void Clear()
        {
            foreach (var image in _images.Values)
                image.Bitmap.Dispose();
            _images.Clear();
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Clear();
        }
    }

    private sealed class ColorBucket
    {
        public long Count;
        public long Red;
        public long Green;
        public long Blue;
    }

    private readonly record struct ColorSample(int Key, Color Color, long Count);

    public static Bitmap RenderFiles(IReadOnlyList<string> paths, AppSettings settings, CancellationToken cancellationToken = default)
    {
        ValidateRequest(paths, settings);
        cancellationToken.ThrowIfCancellationRequested();
        var loaded = LoadImages(paths, cancellationToken);
        try
        {
            return RenderBitmaps(loaded.Select(item => item.Bitmap).ToArray(), settings, cancellationToken);
        }
        finally
        {
            foreach (var item in loaded)
                item.Bitmap.Dispose();
        }
    }

    public static Bitmap RenderPreviewFiles(IReadOnlyList<string> paths, AppSettings settings, PreviewImageCache cache, CancellationToken cancellationToken = default)
    {
        ValidateRequest(paths, settings);
        var previewSettings = settings.Copy();
        previewSettings.OutputWidth = Math.Min(previewSettings.OutputWidth, MaximumPreviewLongEdge);
        return RenderBitmaps(cache.Load(paths, cancellationToken), previewSettings, cancellationToken, MaximumPreviewShadowMaskLongEdge);
    }

    public static string Generate(IReadOnlyList<string> paths, AppSettings settings, CancellationToken cancellationToken = default)
    {
        using var image = RenderFiles(paths, settings, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        using var encoded = new MemoryStream();
        image.Save(encoded, ImageFormat.Png);
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = encoded.ToArray();
        cancellationToken.ThrowIfCancellationRequested();
        var folder = Path.GetDirectoryName(Path.GetFullPath(paths[0]))!;
        var baseName = paths.Count == 1
            ? $"{Path.GetFileNameWithoutExtension(paths[0])}-softstage"
            : "softstage-composition";

        for (var suffix = 1; ; suffix++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var name = suffix == 1 ? $"{baseName}.png" : $"{baseName}-{suffix}.png";
            var destination = Path.Combine(folder, name);
            if (File.Exists(destination))
                continue;
            var temporary = Path.Combine(folder, $".backdrop-{Guid.NewGuid():N}.tmp");
            var temporaryCreated = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 64 * 1024, FileOptions.SequentialScan))
                {
                    temporaryCreated = true;
                    var offset = 0;
                    while (offset < bytes.Length)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var count = Math.Min(64 * 1024, bytes.Length - offset);
                        stream.Write(bytes, offset, count);
                        offset += count;
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    stream.Flush(flushToDisk: true);
                    cancellationToken.ThrowIfCancellationRequested();
                }

                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    File.Move(temporary, destination);
                }
                catch (IOException) when (File.Exists(destination))
                {
                    continue;
                }
                return destination;
            }
            finally
            {
                if (temporaryCreated)
                    try { File.Delete(temporary); } catch { }
            }
        }
    }

    public static Bitmap CreateSamplePreview(AppSettings settings)
    {
        return RenderBitmaps(SampleArtwork.Value, settings, shadowMaskLongEdge: MaximumPreviewShadowMaskLongEdge);
    }

    public static Bitmap CreateSamplePreview(AppSettings settings, int maximumLongEdge)
    {
        var previewSettings = settings.Copy();
        previewSettings.OutputWidth = Math.Min(previewSettings.OutputWidth, maximumLongEdge);
        return CreateSamplePreview(previewSettings);
    }

    public static List<Bitmap> CreateSampleInputs(string directory)
    {
        Directory.CreateDirectory(directory);
        var images = new List<Bitmap>
        {
            CreateSampleImage(420, 680, Color.FromArgb(39, 86, 117), Color.FromArgb(237, 182, 106), 0),
            CreateSampleImage(420, 560, Color.FromArgb(55, 104, 91), Color.FromArgb(225, 190, 125), 1),
            CreateSampleImage(680, 420, Color.FromArgb(75, 75, 112), Color.FromArgb(240, 156, 117), 2),
            CreateSampleImage(440, 700, Color.FromArgb(83, 85, 111), Color.FromArgb(234, 160, 111), 1)
        };
        var names = new[] { "portrait one.png", "portrait two.png", "landscape.png", "portrait three café.png" };
        try
        {
            for (var i = 0; i < images.Count; i++)
                images[i].Save(Path.Combine(directory, names[i]), ImageFormat.Png);
            return images;
        }
        catch
        {
            foreach (var image in images)
                image.Dispose();
            throw;
        }
    }

    public static IReadOnlyList<RectangleF> GetImageBounds(IReadOnlyList<Size> imageSizes, AppSettings settings)
    {
        ValidateGeometry(imageSizes, settings);
        return GetImageBounds(imageSizes, settings, GetCanvasDimensions(imageSizes, settings));
    }

    public static Size GetCanvasDimensions(IReadOnlyList<Size> imageSizes, AppSettings settings)
    {
        ValidateGeometry(imageSizes, settings);
        var layout = ResolveLayout(imageSizes, settings.Layout);
        var ratio = settings.CanvasRatio switch
        {
            CanvasRatio.Wide16x9 => 16d / 9,
            CanvasRatio.Square1x1 => 1,
            CanvasRatio.Portrait4x5 => 4d / 5,
            _ when imageSizes.Count == 1 => imageSizes[0].Width / (double)imageSizes[0].Height,
            _ when layout == CompositionLayout.Row => NaturalRowRatio(imageSizes),
            _ => NaturalGridRatio(imageSizes)
        };
        var longEdge = settings.OutputWidth;
        return ratio >= 1
            ? new Size(longEdge, Math.Max(1, (int)Math.Round(longEdge / ratio)))
            : new Size(Math.Max(1, (int)Math.Round(longEdge * ratio)), longEdge);
    }

    private static IReadOnlyList<RectangleF> GetImageBounds(IReadOnlyList<Size> imageSizes, AppSettings settings, Size canvas)
    {
        var width = canvas.Width;
        var height = canvas.Height;
        var paddingX = width * settings.PaddingPercent / 100f;
        var paddingY = height * settings.PaddingPercent / 100f;
        var content = new RectangleF(paddingX, paddingY, width - 2 * paddingX, height - 2 * paddingY);

        if (imageSizes.Count == 1)
        {
            var box = Fit(imageSizes[0], content.Width, content.Height);
            return [Center(box, content)];
        }

        if (ResolveLayout(imageSizes, settings.Layout) == CompositionLayout.Row)
        {
            var totalRatio = imageSizes.Sum(size => size.Width / (float)size.Height);
            var gapRatio = 0.04f;
            var imageHeight = Math.Min(content.Height, content.Width / (totalRatio + gapRatio * (imageSizes.Count - 1)));
            var gap = imageHeight * gapRatio;
            var totalWidth = totalRatio * imageHeight + gap * (imageSizes.Count - 1);
            var left = content.Left + (content.Width - totalWidth) / 2;
            var bounds = new List<RectangleF>(imageSizes.Count);
            foreach (var size in imageSizes)
            {
                var imageWidth = size.Width / (float)size.Height * imageHeight;
                bounds.Add(new RectangleF(left, content.Top + (content.Height - imageHeight) / 2, imageWidth, imageHeight));
                left += imageWidth + gap;
            }
            return bounds;
        }

        var columns = imageSizes.Count <= 4 ? 2 : 3;
        var rows = (int)Math.Ceiling(imageSizes.Count / (double)columns);
        var gapX = content.Width * 0.035f;
        var gapY = content.Height * 0.05f;
        var cellWidth = (content.Width - gapX * (columns - 1)) / columns;
        var cellHeight = (content.Height - gapY * (rows - 1)) / rows;
        var result = new List<RectangleF>(imageSizes.Count);
        for (var i = 0; i < imageSizes.Count; i++)
        {
            var row = i / columns;
            var rowItemCount = Math.Min(columns, imageSizes.Count - row * columns);
            var rowWidth = rowItemCount * cellWidth + (rowItemCount - 1) * gapX;
            var cell = new RectangleF(
                content.Left + (content.Width - rowWidth) / 2 + (i % columns) * (cellWidth + gapX),
                content.Top + row * (cellHeight + gapY),
                cellWidth,
                cellHeight);
            result.Add(Center(Fit(imageSizes[i], cell.Width, cell.Height), cell));
        }
        return result;
    }

    private static void ValidateGeometry(IReadOnlyList<Size> imageSizes, AppSettings settings)
    {
        if (imageSizes.Count is < 1 or > MaximumImages)
            throw new ArgumentOutOfRangeException(nameof(imageSizes), $"Choose between one and {MaximumImages} images.");
        if (!settings.IsValid())
            throw new ArgumentOutOfRangeException(nameof(settings));
        if (imageSizes.Any(size => size.Width <= 0 || size.Height <= 0))
            throw new ArgumentException("Image dimensions must be positive.", nameof(imageSizes));
    }

    private static CompositionLayout ResolveLayout(IReadOnlyList<Size> imageSizes, CompositionLayout layout) =>
        imageSizes.Count == 1 ? CompositionLayout.Row : layout == CompositionLayout.Auto
            ? imageSizes.Count <= 4 && imageSizes.All(size => size.Height > size.Width) ? CompositionLayout.Row : CompositionLayout.Grid
            : layout;

    private static double NaturalRowRatio(IReadOnlyList<Size> imageSizes) => Math.Clamp(
        imageSizes.Sum(size => size.Width / (double)size.Height) + 0.04 * (imageSizes.Count - 1),
        MinimumAutoCompositionRatio,
        MaximumAutoCompositionRatio);

    private static double NaturalGridRatio(IReadOnlyList<Size> imageSizes)
    {
        var columns = imageSizes.Count <= 4 ? 2 : 3;
        var rows = (int)Math.Ceiling(imageSizes.Count / (double)columns);
        var averageImageRatio = imageSizes.Average(size => size.Width / (double)size.Height);
        return Math.Clamp(columns * averageImageRatio / rows, MinimumAutoCompositionRatio, MaximumAutoCompositionRatio);
    }

    private static void ValidateRequest(IReadOnlyList<string> paths, AppSettings settings)
    {
        if (paths.Count is < 1 or > MaximumImages)
            throw new ArgumentOutOfRangeException(nameof(paths), $"Choose between one and {MaximumImages} images.");
        if (!settings.IsValid())
            throw new ArgumentOutOfRangeException(nameof(settings));
    }

    private static List<LoadedImage> LoadImages(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        var infos = new List<ImageInfo>(paths.Count);
        long totalPixels = 0;
        foreach (var candidate in paths)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = Path.GetFullPath(candidate);
            if (!File.Exists(path))
                throw new FileNotFoundException($"The selected image was not found: {candidate}", path);
            if (!SupportedExtensions.Contains(Path.GetExtension(path)))
                throw new InvalidDataException($"'{Path.GetFileName(path)}' is not a supported image. Choose PNG, JPEG, BMP, GIF, or TIFF files.");

            try
            {
                using var source = Image.FromFile(path);
                var pixels = (long)source.Width * source.Height;
                if (pixels > MaximumImagePixels)
                    throw new InvalidDataException($"'{Path.GetFileName(path)}' is larger than the 40-megapixel limit.");
                totalPixels += pixels;
                if (totalPixels > MaximumSelectionPixels)
                    throw new InvalidDataException("The selected images exceed the 80-megapixel total limit.");
                infos.Add(new ImageInfo(path, ReadOrientation(source)));
            }
            catch (InvalidDataException)
            {
                throw;
            }
            catch (Exception ex) when (ex is ArgumentException or OutOfMemoryException or ExternalException or IOException)
            {
                throw new InvalidDataException($"'{Path.GetFileName(path)}' could not be decoded as an image. Check the file and try again.", ex);
            }
        }

        var loaded = new List<LoadedImage>(infos.Count);
        try
        {
            foreach (var info in infos)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using var source = Image.FromFile(info.Path);
                Bitmap? bitmap = null;
                try
                {
                    bitmap = new Bitmap(source);
                    ApplyOrientation(bitmap, info.Orientation);
                    loaded.Add(new LoadedImage(info.Path, bitmap));
                    bitmap = null;
                }
                finally
                {
                    bitmap?.Dispose();
                }
            }
            return loaded;
        }
        catch (Exception ex)
        {
            foreach (var image in loaded)
                image.Bitmap.Dispose();
            if (ex is ArgumentException or OutOfMemoryException or ExternalException or IOException)
                throw new InvalidDataException("Softstage could not load the selected images. Check that they are valid and try again.", ex);
            throw;
        }
    }

    private static Size PreviewSize(Size source)
    {
        var scale = Math.Min(1d, MaximumPreviewLongEdge / (double)Math.Max(source.Width, source.Height));
        return new Size(Math.Max(1, (int)Math.Round(source.Width * scale)), Math.Max(1, (int)Math.Round(source.Height * scale)));
    }

    private static int ReadOrientation(Image image)
    {
        const int OrientationId = 0x0112;
        try
        {
            var value = image.GetPropertyItem(OrientationId)?.Value;
            return value is { Length: >= 2 } ? BitConverter.ToUInt16(value, 0) : 1;
        }
        catch (ArgumentException)
        {
            return 1;
        }
        catch (ExternalException)
        {
            return 1;
        }
    }

    private static void ApplyOrientation(Bitmap bitmap, int orientation)
    {
        var transform = orientation switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.Rotate180FlipX,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        };
        if (transform != RotateFlipType.RotateNoneFlipNone)
            bitmap.RotateFlip(transform);
    }

    private static Bitmap RenderBitmaps(IReadOnlyList<Bitmap> images, AppSettings settings, CancellationToken cancellationToken = default,
        int shadowMaskLongEdge = 384)
    {
        if (!settings.IsValid())
            throw new ArgumentOutOfRangeException(nameof(settings));
        cancellationToken.ThrowIfCancellationRequested();
        var sizes = images.Select(image => image.Size).ToArray();
        var canvas = GetCanvasDimensions(sizes, settings);
        var bounds = GetImageBounds(sizes, settings, canvas);
        var output = new Bitmap(canvas.Width, canvas.Height, PixelFormat.Format32bppArgb);
        try
        {
            DrawBackground(output, images, settings, cancellationToken);

            using var graphics = Graphics.FromImage(output);
            graphics.CompositingQuality = CompositingQuality.HighQuality;
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            graphics.SmoothingMode = SmoothingMode.AntiAlias;

            for (var i = 0; i < images.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DrawShadow(graphics, bounds[i], settings.ShadowStrength, output.Width, shadowMaskLongEdge);
                var radius = Math.Min(2f * Math.Max(canvas.Width, canvas.Height) / 1920f,
                    Math.Min(bounds[i].Width, bounds[i].Height) / 2);
                using var path = BackdropControlPaint.RoundedRectangle(bounds[i], radius);
                using var brush = new TextureBrush(images[i], WrapMode.Clamp);
                using var transform = new Matrix(bounds[i].Width / images[i].Width, 0, 0,
                    bounds[i].Height / images[i].Height, bounds[i].X, bounds[i].Y);
                brush.Transform = transform;
                graphics.FillPath(brush, path);
            }
            return output;
        }
        catch
        {
            output.Dispose();
            throw;
        }
    }

    private static void DrawBackground(Bitmap canvas, IReadOnlyList<Bitmap> images, AppSettings settings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var first = ColorTranslator.FromHtml(settings.BackgroundColor1Hex);
        switch (settings.BackgroundMode)
        {
            case BackgroundMode.AutoGradient:
                var sampled = SampleDominantColors(images, settings.AutoGradientLightenPercent);
                FillGradient(canvas, sampled.First, sampled.Second, cancellationToken);
                break;
            case BackgroundMode.SolidColor:
                using (var graphics = Graphics.FromImage(canvas))
                    graphics.Clear(first);
                break;
            case BackgroundMode.CustomGradient:
                FillGradient(canvas, first, ColorTranslator.FromHtml(settings.BackgroundColor2Hex), cancellationToken);
                break;
            case BackgroundMode.Pattern:
                using (var graphics = Graphics.FromImage(canvas))
                using (var tile = CreatePatternTile(canvas.Size, first, settings.BackgroundPattern, cancellationToken))
                using (var brush = new TextureBrush(tile, WrapMode.Tile))
                {
                    graphics.CompositingQuality = CompositingQuality.HighQuality;
                    graphics.SmoothingMode = SmoothingMode.AntiAlias;
                    graphics.Clear(first);
                    graphics.FillRectangle(brush, 0, 0, canvas.Width, canvas.Height);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(settings), "The background mode is invalid.");
        }
    }

    private static void FillGradient(Bitmap canvas, Color first, Color second, CancellationToken cancellationToken)
    {
        if (first.ToArgb() == second.ToArgb())
        {
            using var graphics = Graphics.FromImage(canvas);
            graphics.Clear(first);
            return;
        }

        var data = canvas.LockBits(new Rectangle(0, 0, canvas.Width, canvas.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        try
        {
            var stride = Math.Abs(data.Stride);
            var row = new byte[stride];
            var lastX = canvas.Width - 1;
            var denominator = Math.Max(1, lastX);

            for (var y = 0; y < canvas.Height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < canvas.Width; x++)
                {
                    var pixel = x * 4;
                    if (x == 0 || x == lastX)
                    {
                        var endpoint = x == 0 ? first : second;
                        row[pixel] = endpoint.B;
                        row[pixel + 1] = endpoint.G;
                        row[pixel + 2] = endpoint.R;
                    }
                    else
                    {
                        var amount = x / (float)denominator;
                        var noise = GradientNoise(x, y);
                        row[pixel] = DitheredChannel(first.B, second.B, amount, noise);
                        row[pixel + 1] = DitheredChannel(first.G, second.G, amount, noise);
                        row[pixel + 2] = DitheredChannel(first.R, second.R, amount, noise);
                    }
                    row[pixel + 3] = 255;
                }

                Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
            }
        }
        finally
        {
            canvas.UnlockBits(data);
        }
    }

    private static byte DitheredChannel(byte first, byte second, float amount, uint noise)
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

    private static Bitmap CreatePatternTile(Size canvasSize, Color baseColor, BackgroundPattern pattern, CancellationToken cancellationToken)
    {
        if (pattern == BackgroundPattern.SoftGrain)
            return CreateGrainTile(baseColor, cancellationToken);
        if (pattern != BackgroundPattern.Dots)
            throw new ArgumentOutOfRangeException(nameof(pattern));

        var longEdge = Math.Max(canvasSize.Width, canvasSize.Height);
        var spacing = Math.Clamp(longEdge / 50, 12, 72);
        var tileSize = spacing * 4;
        var radius = Math.Clamp(longEdge / 1500f, 0.8f, 2.5f);
        var tile = new Bitmap(tileSize, tileSize, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(tile);
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            graphics.Clear(Color.Transparent);
            var lightBackground = baseColor.GetBrightness() >= 0.5f;
            var contrast = lightBackground ? Color.Black : Color.White;
            using var brush = new SolidBrush(Color.FromArgb(lightBackground ? 36 : 18, contrast));
            for (var y = spacing / 2f; y < tileSize; y += spacing)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = spacing / 2f; x < tileSize; x += spacing)
                    graphics.FillEllipse(brush, x - radius, y - radius, radius * 2, radius * 2);
            }
            return tile;
        }
        catch
        {
            tile.Dispose();
            throw;
        }
    }

    private static Bitmap CreateGrainTile(Color baseColor, CancellationToken cancellationToken)
    {
        const int size = 128;
        var tile = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        var data = tile.LockBits(new Rectangle(0, 0, size, size), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
        var complete = false;
        try
        {
            var row = new byte[Math.Abs(data.Stride)];
            var random = new Random(0x5EED);
            static byte Shade(byte component, int offset) => (byte)Math.Clamp(
                offset >= 0 ? component + (255 - component) * offset / 64 : component + component * offset / 64,
                0, 255);

            for (var y = 0; y < size; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (var x = 0; x < size; x++)
                {
                    var offset = random.Next(-64, 65);
                    var pixel = x * 4;
                    row[pixel] = Shade(baseColor.B, offset);
                    row[pixel + 1] = Shade(baseColor.G, offset);
                    row[pixel + 2] = Shade(baseColor.R, offset);
                    row[pixel + 3] = (byte)random.Next(7, 17);
                }
                Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), row.Length);
            }
            complete = true;
        }
        finally
        {
            tile.UnlockBits(data);
            if (!complete)
                tile.Dispose();
        }
        return tile;
    }

    private static (Color First, Color Second) SampleDominantColors(IReadOnlyList<Bitmap> images, int lightenPercent)
    {
        var buckets = new Dictionary<int, ColorBucket>();
        foreach (var image in images)
        {
            const int samplesPerAxis = 24;
            for (var y = 0; y < samplesPerAxis; y++)
            for (var x = 0; x < samplesPerAxis; x++)
            {
                var color = image.GetPixel(
                    Math.Min(image.Width - 1, x * image.Width / samplesPerAxis),
                    Math.Min(image.Height - 1, y * image.Height / samplesPerAxis));
                if (color.A < 24)
                    continue;

                var key = (color.R >> 3) << 10 | (color.G >> 3) << 5 | color.B >> 3;
                if (!buckets.TryGetValue(key, out var bucket))
                    buckets[key] = bucket = new ColorBucket();
                bucket.Count++;
                bucket.Red += color.R;
                bucket.Green += color.G;
                bucket.Blue += color.B;
            }
        }

        var dominant = buckets.OrderBy(pair => pair.Key)
            .Select(pair => new ColorSample(pair.Key, Average(pair.Value), pair.Value.Count))
            .ToArray();
        if (dominant.Length == 0)
            return (LightenAutomaticColor(Color.FromArgb(91, 104, 122), lightenPercent),
                LightenAutomaticColor(Color.FromArgb(145, 153, 166), lightenPercent));

        var colored = dominant.Where(item => item.Color.GetSaturation() >= 0.2f && item.Color.GetBrightness() >= 0.08f).ToArray();
        var mostSupportedColor = colored.OrderByDescending(item => item.Count).ThenBy(item => item.Key).FirstOrDefault();
        var candidates = colored.Length > 0 && mostSupportedColor.Count >= dominant.Sum(item => item.Count) * 0.05
            ? colored
            : dominant;
        var firstSeed = candidates.OrderByDescending(item => item.Count).ThenBy(item => item.Key).First();
        var secondSeed = candidates.Where(item => item.Key != firstSeed.Key)
            .OrderByDescending(item => item.Count * ColorDistanceSquared(firstSeed.Color, item.Color))
            .ThenBy(item => item.Key)
            .FirstOrDefault();
        var refined = secondSeed.Count == 0
            ? (firstSeed.Color, Color.Empty)
            : RefineColorClusters(candidates, firstSeed.Color, secondSeed.Color);
        var first = refined.Item1;
        var second = refined.Item2;
        if (second.IsEmpty || ColorDistance(first, second) < 50)
            second = first.GetBrightness() < 0.7f
                ? Blend(first, Color.White, 0.32f)
                : Blend(first, Color.Black, 0.25f);
        return (LightenAutomaticColor(first, lightenPercent), LightenAutomaticColor(second, lightenPercent));
    }

    private static (Color First, Color Second) RefineColorClusters(ColorSample[] samples, Color first, Color second)
    {
        for (var iteration = 0; iteration < 6; iteration++)
        {
            var firstCluster = new ColorBucket();
            var secondCluster = new ColorBucket();
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
            if (nextFirst.ToArgb() == first.ToArgb() && nextSecond.ToArgb() == second.ToArgb())
                break;
            first = nextFirst;
            second = nextSecond;
        }
        return (first, second);
    }

    private static Color LightenAutomaticColor(Color color, int percent) =>
        Blend(color, Color.White, percent / 100f);

    private static Color Average(ColorBucket bucket) => Color.FromArgb(
        (int)(bucket.Red / bucket.Count),
        (int)(bucket.Green / bucket.Count),
        (int)(bucket.Blue / bucket.Count));

    private static double ColorDistance(Color first, Color second)
    {
        return Math.Sqrt(ColorDistanceSquared(first, second));
    }

    private static double ColorDistanceSquared(Color first, Color second)
    {
        var red = first.R - second.R;
        var green = first.G - second.G;
        var blue = first.B - second.B;
        return red * red + green * green + blue * blue;
    }

    private static void DrawShadow(Graphics graphics, RectangleF bounds, double strength, int canvasWidth, int shadowMaskLongEdge)
    {
        if (strength <= 0)
            return;

        var blur = Math.Clamp(canvasWidth * 0.022f, 12, 80);
        var offset = 0f;
        var sigma = blur * 0.65f;
        var padding = (int)Math.Ceiling(sigma * 3f + offset);
        var scale = Math.Min(1f, shadowMaskLongEdge / Math.Max(bounds.Width, bounds.Height));
        var width = Math.Max(1, (int)Math.Ceiling((bounds.Width + padding * 2) * scale));
        var height = Math.Max(1, (int)Math.Ceiling((bounds.Height + padding * 2) * scale));
        using var mask = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using (var maskGraphics = Graphics.FromImage(mask))
        {
            maskGraphics.Clear(Color.Transparent);
            using var brush = new SolidBrush(Color.FromArgb(Math.Clamp((int)Math.Round(strength * 120), 0, 255), Color.Black));
            maskGraphics.FillRectangle(brush, padding * scale, (padding + offset) * scale, bounds.Width * scale, bounds.Height * scale);
        }

        BlurShadowMask(mask, sigma * scale);
        graphics.DrawImage(mask,
            new RectangleF(bounds.X - padding, bounds.Y - padding, bounds.Width + padding * 2, bounds.Height + padding * 2),
            new Rectangle(0, 0, width, height),
            GraphicsUnit.Pixel);
    }

    private static void BlurShadowMask(Bitmap mask, float sigma)
    {
        if (sigma < 0.5f)
            return;

        var radius = (int)Math.Ceiling(sigma * 3);
        var kernel = Enumerable.Range(-radius, radius * 2 + 1)
            .Select(offset => (float)Math.Exp(-(offset * offset) / (2 * sigma * sigma)))
            .ToArray();
        var weight = kernel.Sum();
        for (var i = 0; i < kernel.Length; i++)
            kernel[i] /= weight;

        var data = mask.LockBits(new Rectangle(0, 0, mask.Width, mask.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb);
        try
        {
            var stride = Math.Abs(data.Stride);
            var bytes = new byte[stride * mask.Height];
            var row = new byte[stride];
            for (var y = 0; y < mask.Height; y++)
            {
                Marshal.Copy(IntPtr.Add(data.Scan0, y * data.Stride), row, 0, stride);
                Buffer.BlockCopy(row, 0, bytes, y * stride, stride);
            }

            var source = new byte[mask.Width * mask.Height];
            var horizontal = new byte[source.Length];
            var blurred = new byte[source.Length];
            for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width; x++)
                source[y * mask.Width + x] = bytes[y * stride + x * 4 + 3];

            for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width; x++)
            {
                var value = 0f;
                for (var k = -radius; k <= radius; k++)
                {
                    var sampleX = x + k;
                    if ((uint)sampleX < (uint)mask.Width)
                        value += source[y * mask.Width + sampleX] * kernel[k + radius];
                }
                horizontal[y * mask.Width + x] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
            }

            for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width; x++)
            {
                var value = 0f;
                for (var k = -radius; k <= radius; k++)
                {
                    var sampleY = y + k;
                    if ((uint)sampleY < (uint)mask.Height)
                        value += horizontal[sampleY * mask.Width + x] * kernel[k + radius];
                }
                blurred[y * mask.Width + x] = (byte)Math.Clamp((int)Math.Round(value), 0, 255);
            }

            for (var y = 0; y < mask.Height; y++)
            for (var x = 0; x < mask.Width; x++)
            {
                var pixel = y * stride + x * 4;
                bytes[pixel] = bytes[pixel + 1] = bytes[pixel + 2] = 0;
                bytes[pixel + 3] = blurred[y * mask.Width + x];
            }

            for (var y = 0; y < mask.Height; y++)
            {
                Buffer.BlockCopy(bytes, y * stride, row, 0, stride);
                Marshal.Copy(row, 0, IntPtr.Add(data.Scan0, y * data.Stride), stride);
            }
        }
        finally
        {
            mask.UnlockBits(data);
        }
    }

    private static Bitmap CreateSampleImage(int width, int height, Color sky, Color accent, int variation)
    {
        var image = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(image);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var skyBrush = new LinearGradientBrush(new Rectangle(0, 0, width, height), sky, Blend(sky, Color.Black, 0.45f), LinearGradientMode.Vertical))
            graphics.FillRectangle(skyBrush, 0, 0, width, height);
        using (var sunBrush = new SolidBrush(Color.FromArgb(215, accent)))
            graphics.FillEllipse(sunBrush, width * 0.59f, height * (0.18f + variation * 0.02f), width * 0.17f, width * 0.17f);
        using var far = new SolidBrush(Blend(sky, accent, 0.22f));
        using var near = new SolidBrush(Blend(sky, Color.FromArgb(23, 35, 48), 0.45f));
        graphics.FillPolygon(far,
        [
            new PointF(0, height * 0.67f), new PointF(width * 0.28f, height * 0.36f),
            new PointF(width * 0.5f, height * 0.65f), new PointF(width * 0.72f, height * 0.4f),
            new PointF(width, height * 0.69f), new PointF(width, height), new PointF(0, height)
        ]);
        graphics.FillPolygon(near,
        [
            new PointF(0, height * 0.78f), new PointF(width * 0.32f, height * 0.58f),
            new PointF(width * 0.57f, height * 0.82f), new PointF(width * 0.84f, height * 0.61f),
            new PointF(width, height * 0.76f), new PointF(width, height), new PointF(0, height)
        ]);
        return image;
    }

    private static Bitmap[] LoadSampleArtwork() =>
    [
        LoadSampleImage("Backdrop.Samples.stairway.png"),
        LoadSampleImage("Backdrop.Samples.arch.png"),
        LoadSampleImage("Backdrop.Samples.sphere.png")
    ];

    private static Bitmap LoadSampleImage(string resourceName)
    {
        using var stream = typeof(BackdropRenderer).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded sample image '{resourceName}' is missing.");
        using var source = Image.FromStream(stream, useEmbeddedColorManagement: true, validateImageData: true);
        var image = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppPArgb);
        using var graphics = Graphics.FromImage(image);
        graphics.CompositingMode = CompositingMode.SourceCopy;
        graphics.DrawImage(source,
            new Rectangle(0, 0, source.Width, source.Height),
            0, 0, source.Width, source.Height, GraphicsUnit.Pixel);
        return image;
    }
    private static Color Blend(Color from, Color to, float amount) => Color.FromArgb(
        (int)(from.R + (to.R - from.R) * amount),
        (int)(from.G + (to.G - from.G) * amount),
        (int)(from.B + (to.B - from.B) * amount));

    private static RectangleF Fit(Size image, float width, float height)
    {
        var scale = Math.Min(width / image.Width, height / image.Height);
        return new RectangleF(0, 0, image.Width * scale, image.Height * scale);
    }

    private static RectangleF Center(RectangleF size, RectangleF bounds) => new(
        bounds.Left + (bounds.Width - size.Width) / 2,
        bounds.Top + (bounds.Height - size.Height) / 2,
        size.Width,
        size.Height);
}

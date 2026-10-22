using Avalonia.Platform.Storage;

namespace Softstage.Mac.Services;

/// <summary>The image types Softstage.Core decodes (ImageLoader.SupportedExtensions).</summary>
internal static class ImageFiles
{
    public static readonly string[] Extensions = [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff"];

    public static bool IsSupported(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    public static FilePickerFileType PickerFilter { get; } = new("Images")
    {
        Patterns = Extensions.Select(extension => "*" + extension).ToArray(),
        MimeTypes = ["image/*"],
        AppleUniformTypeIdentifiers = ["public.image"]
    };
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Softstage.Core;

public static class SettingsStore
{
    private const string CorruptWarning =
        "Softstage could not read its saved preferences. It will use the defaults until you save new preferences.";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        Converters =
        {
            new JsonStringEnumConverter<CanvasRatio>(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
            new JsonStringEnumConverter<CompositionLayout>(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
            new JsonStringEnumConverter<BackgroundMode>(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
            new JsonStringEnumConverter<BackgroundPattern>(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        }
    };

    /// <summary>
    /// Reads settings from <paramref name="filePath"/>. A missing file gives defaults with no warning.
    /// A file that cannot be parsed or holds out-of-range values gives defaults plus a warning.
    /// </summary>
    public static (AppSettings Settings, string? Warning) Load(string filePath)
    {
        if (!File.Exists(filePath))
            return (new AppSettings(), null);

        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(filePath), JsonOptions);
            if (settings is null || !settings.IsValid())
                return Corrupt();

            return (settings, null);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return Corrupt();
        }
    }

    /// <summary>Writes settings atomically: a temporary file in the same folder, then a move over the target.</summary>
    public static void Save(AppSettings settings, string filePath)
    {
        if (!settings.IsValid())
            throw new SoftstageException("Settings are outside the supported range.");

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory, $"settings.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var stream = new FileStream(temporaryPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, settings, JsonOptions);
                stream.Flush(flushToDisk: true);
            }

            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        catch
        {
            try { File.Delete(temporaryPath); } catch { }
            throw;
        }
    }

    private static (AppSettings Settings, string? Warning) Corrupt() => (new AppSettings(), CorruptWarning);
}

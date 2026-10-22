namespace Softstage.Mac.Services;

/// <summary>Where Softstage keeps its files. The single place that decides folder locations.</summary>
internal static class AppPaths
{
    /// <summary>Tests set this to a temporary file so they never touch the real settings. Null in normal runs.</summary>
    internal static string? SettingsFileOverride { get; set; }

    /// <summary>The settings file in use: the override when set, otherwise <see cref="DefaultSettingsFile"/>.</summary>
    public static string SettingsFile => SettingsFileOverride ?? DefaultSettingsFile;

    /// <summary>On macOS: ~/Library/Application Support/Softstage/settings.json. Elsewhere (dev runs): %LOCALAPPDATA%\Softstage\settings.json.</summary>
    public static string DefaultSettingsFile =>
        OperatingSystem.IsMacOS()
            ? Path.Combine(HomeFolder, "Library", "Application Support", "Softstage", "settings.json")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Softstage", "settings.json");

    public static string HomeFolder => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}

namespace Softstage.Mac.Services;

/// <summary>Where Softstage keeps its files. The single place that decides folder locations.</summary>
internal static class AppPaths
{
    /// <summary>On macOS: ~/Library/Application Support/Softstage/settings.json. Elsewhere (dev runs): %LOCALAPPDATA%\Softstage\settings.json.</summary>
    public static string SettingsFile =>
        OperatingSystem.IsMacOS()
            ? Path.Combine(HomeFolder, "Library", "Application Support", "Softstage", "settings.json")
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Softstage", "settings.json");

    public static string HomeFolder => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
}

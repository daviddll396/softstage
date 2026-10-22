using System.Security;
using Softstage.Core;

namespace Softstage.Mac.Services;

/// <summary>
/// Installs or removes the Finder Quick Action in ~/Library/Services. The workflow template is copied
/// from the app bundle, and the executable path is written into it.
/// </summary>
internal static class FinderQuickAction
{
    public const string BundleName = "Create composition with Softstage.workflow";
    public const string Placeholder = "__SOFTSTAGE_EXECUTABLE__";

    // Characters that would break the double-quoted command in document.wflow.
    private static readonly char[] UnsafePathCharacters = ['"', '$', '`', '\\'];

    /// <summary>Tests set this to a temporary folder so they never write to the real Services folder. Null in normal runs.</summary>
    internal static string? ServicesFolderOverride { get; set; }

    /// <summary>Tests set this so the macOS-only checks can run on other systems. Null in normal runs.</summary>
    internal static bool? IsMacOSOverride { get; set; }

    private static bool IsMacOS => IsMacOSOverride ?? OperatingSystem.IsMacOS();

    public static string ServicesFolder => ServicesFolderOverride ?? Path.Combine(AppPaths.HomeFolder, "Library", "Services");

    public static string InstalledPath => Path.Combine(ServicesFolder, BundleName);

    public static bool IsInstalled => Directory.Exists(InstalledPath);

    public static void Install(string executablePath)
    {
        if (!IsMacOS)
            throw new SoftstageException("Finder right-click is available on macOS only.");

        if (Path.GetFileName(executablePath).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
            throw new SoftstageException("Turn on Finder right-click from the packaged Softstage app, not from a development run.");

        if (executablePath.IndexOfAny(UnsafePathCharacters) >= 0)
            throw new SoftstageException(
                "Finder right-click cannot be turned on from this location, because the app path contains a double quote, $, a backtick, or a backslash. Move Softstage to a folder with a plain path and try again.");

        var template = FindTemplate();
        var destination = InstalledPath;

        if (Directory.Exists(destination))
            Directory.Delete(destination, recursive: true);

        CopyDirectory(template, destination);

        // The executable path is inserted as XML text, because document.wflow is a plist.
        var escapedPath = SecurityElement.Escape(executablePath) ?? executablePath;
        foreach (var file in Directory.EnumerateFiles(destination, "*", SearchOption.AllDirectories))
        {
            var text = File.ReadAllText(file);
            if (text.Contains(Placeholder, StringComparison.Ordinal))
                File.WriteAllText(file, text.Replace(Placeholder, escapedPath, StringComparison.Ordinal));
        }
    }

    public static void Remove()
    {
        if (Directory.Exists(InstalledPath))
            Directory.Delete(InstalledPath, recursive: true);
    }

    private static string FindTemplate()
    {
        // Packaged build: Contents/MacOS/.. -> Contents/Resources/QuickAction.
        var bundled = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "Resources", "QuickAction", BundleName));
        if (Directory.Exists(bundled))
            return bundled;

        // Development run outside a bundle: the source template in the repository.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, "mac", "packaging", "quick-action", BundleName);
            if (Directory.Exists(candidate))
                return candidate;
        }

        throw new SoftstageException("The Finder action template is missing from this build of Softstage.");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.EnumerateFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)));

        foreach (var directory in Directory.EnumerateDirectories(source))
            CopyDirectory(directory, Path.Combine(destination, Path.GetFileName(directory)));
    }
}

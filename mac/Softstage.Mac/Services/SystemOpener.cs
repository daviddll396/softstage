using System.Diagnostics;

namespace Softstage.Mac.Services;

/// <summary>Opens a file or reveals it in Finder. Arguments go through ArgumentList, never string concatenation.</summary>
internal static class SystemOpener
{
    public static void OpenFile(string path) => Start(path, reveal: false);

    public static void ShowInFolder(string path) => Start(path, reveal: true);

    private static void Start(string path, bool reveal)
    {
        if (OperatingSystem.IsMacOS())
        {
            var startInfo = new ProcessStartInfo("open") { UseShellExecute = false };
            if (reveal)
                startInfo.ArgumentList.Add("-R");
            startInfo.ArgumentList.Add(path);
            Process.Start(startInfo)?.Dispose();
            return;
        }

        // Dev runs on other systems: hand the path to the default shell handler.
        var target = reveal ? Path.GetDirectoryName(path) ?? path : path;
        Process.Start(new ProcessStartInfo(target) { UseShellExecute = true })?.Dispose();
    }
}

using Softstage.Mac.Services;
using Xunit;

namespace Softstage.Mac.Tests;

public sealed class AppPathsTests
{
    // xUnit 2 cannot report a skip decided at run time (the 2.x runner shows it as a failure), so this test has
    // one conditional branch per system. macOS CI runs the first branch. Windows runs the second branch.
    [Fact]
    public void DefaultSettingsFile_MatchesTheDocumentedPathForThisSystem()
    {
        var expected = OperatingSystem.IsMacOS()
            ? Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                "Library", "Application Support", "Softstage", "settings.json")
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Softstage", "settings.json");

        Assert.Equal(expected, AppPaths.DefaultSettingsFile);
    }

    [Fact]
    public void SettingsFile_UsesTheInjectedPathWhenSet()
    {
        using var sandbox = new TestSandbox();

        Assert.Equal(sandbox.SettingsFile, AppPaths.SettingsFile);
        Assert.NotEqual(sandbox.SettingsFile, AppPaths.DefaultSettingsFile);
    }
}

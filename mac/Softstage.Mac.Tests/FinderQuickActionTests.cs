using Softstage.Core;
using Softstage.Mac.Services;
using Xunit;

namespace Softstage.Mac.Tests;

public sealed class FinderQuickActionTests
{
    private const string Executable = "/Applications/Softstage.app/Contents/MacOS/Softstage";

    [Fact]
    public void Install_CopiesTemplateAndWritesExecutablePath_WithNoPlaceholderLeft()
    {
        using var sandbox = new TestSandbox();
        FinderQuickAction.IsMacOSOverride = true;

        FinderQuickAction.Install(Executable);

        var bundle = Path.Combine(sandbox.ServicesFolder, FinderQuickAction.BundleName);
        Assert.Equal(bundle, FinderQuickAction.InstalledPath);
        Assert.True(Directory.Exists(bundle));
        Assert.True(File.Exists(Path.Combine(bundle, "Contents", "Info.plist")));
        Assert.True(File.Exists(Path.Combine(bundle, "Contents", "document.wflow")));

        var workflow = File.ReadAllText(Path.Combine(bundle, "Contents", "document.wflow"));
        Assert.Contains("<string>\"/Applications/Softstage.app/Contents/MacOS/Softstage\" --generate \"$@\"</string>", workflow);

        foreach (var file in Directory.EnumerateFiles(bundle, "*", SearchOption.AllDirectories))
            Assert.DoesNotContain(FinderQuickAction.Placeholder, File.ReadAllText(file));
    }

    [Theory]
    [InlineData("/Applications/Soft\"stage/Softstage")]
    [InlineData("/Applications/Soft$stage/Softstage")]
    [InlineData("/Applications/Soft`stage/Softstage")]
    [InlineData("/Applications/Soft\\stage/Softstage")]
    public void Install_PathWithUnsafeCharacter_IsRejectedAndNothingIsWritten(string executable)
    {
        using var sandbox = new TestSandbox();
        FinderQuickAction.IsMacOSOverride = true;

        var error = Assert.Throws<SoftstageException>(() => FinderQuickAction.Install(executable));

        Assert.Contains("double quote, $, a backtick, or a backslash", error.Message);
        Assert.False(Directory.Exists(sandbox.ServicesFolder), "A rejected install must not create the Services folder.");
        Assert.False(FinderQuickAction.IsInstalled);
    }

    [Fact]
    public void Install_OnNonMacOS_IsRejected()
    {
        using var sandbox = new TestSandbox();
        FinderQuickAction.IsMacOSOverride = false;

        var error = Assert.Throws<SoftstageException>(() => FinderQuickAction.Install(Executable));

        Assert.Equal("Finder right-click is available on macOS only.", error.Message);
        Assert.False(Directory.Exists(sandbox.ServicesFolder));
    }

    [Fact]
    public void IsInstalled_ReportsOffThenOnThenOff_AndRemoveDeletesTheBundle()
    {
        using var sandbox = new TestSandbox();
        FinderQuickAction.IsMacOSOverride = true;

        Assert.False(FinderQuickAction.IsInstalled);

        FinderQuickAction.Install(Executable);
        Assert.True(FinderQuickAction.IsInstalled);

        FinderQuickAction.Remove();
        Assert.False(FinderQuickAction.IsInstalled);
        Assert.False(Directory.Exists(FinderQuickAction.InstalledPath));
    }
}

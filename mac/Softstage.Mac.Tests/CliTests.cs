using Xunit;

namespace Softstage.Mac.Tests;

public sealed class CliTests
{
    [Fact]
    public void Run_GenerateThreeImages_ReturnsZeroAndPrintsOutputPath()
    {
        using var sandbox = new TestSandbox();
        var paths = Enumerable.Range(0, 3).Select(i => TestImages.WritePng(sandbox.Images, $"photo-{i + 1}.png", i)).ToArray();
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(new[] { "--generate" }.Concat(paths).ToArray(), stdout, stderr);

        Assert.Equal(0, exitCode);
        var expected = Path.GetFullPath(Path.Combine(sandbox.Images, "softstage-composition.png"));
        Assert.True(File.Exists(expected));
        Assert.Equal(expected, Path.GetFullPath(stdout.ToString().Trim()));
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public void Run_GenerateWithTrackedDemoImages_ReturnsZero()
    {
        // The demo images are git-tracked, so a missing folder is a failure, not a skip.
        var demo = DemoImages.Find();
        Assert.NotNull(demo);

        using var sandbox = new TestSandbox();
        // Copy first, so the output is written to the sandbox and never into the repository.
        var copies = demo.Select(source =>
        {
            var copy = Path.Combine(sandbox.Images, Path.GetFileName(source));
            File.Copy(source, copy);
            return copy;
        }).ToArray();

        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = Program.Run(new[] { "--generate" }.Concat(copies).ToArray(), stdout, stderr);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(Path.Combine(sandbox.Images, "softstage-composition.png")));
        Assert.Equal(string.Empty, stderr.ToString());
    }

    [Fact]
    public void Run_GenerateWithNoImages_ReturnsOneWithMessage()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(new[] { "--generate" }, stdout, stderr);

        Assert.Equal(1, exitCode);
        Assert.Equal("Choose between one and 9 images.", stderr.ToString().Trim());
        Assert.Equal(string.Empty, stdout.ToString());
    }

    [Fact]
    public void Run_UnknownArgument_ReturnsTwoWithUsage()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();

        var exitCode = Program.Run(new[] { "--bogus" }, stdout, stderr);

        Assert.Equal(2, exitCode);
        Assert.Contains("Usage: Softstage", stderr.ToString());
        Assert.Contains("--generate <image>", stderr.ToString());
        Assert.Equal(string.Empty, stdout.ToString());
    }
}

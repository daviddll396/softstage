using System.Globalization;

namespace Softstage.Core;

public enum CanvasRatio { Auto, Wide16x9, Square1x1, Portrait4x5 }

public enum CompositionLayout { Auto, Row, Grid }

public enum BackgroundMode { AutoGradient, SolidColor, CustomGradient, Pattern }

public enum BackgroundPattern { SoftGrain, Dots }

public sealed class AppSettings
{
    public int OutputWidth { get; set; } = 1920;
    public int PaddingPercent { get; set; } = 10;
    public double ShadowStrength { get; set; } = 0.18;
    public CanvasRatio CanvasRatio { get; set; } = CanvasRatio.Wide16x9;
    public CompositionLayout Layout { get; set; } = CompositionLayout.Auto;
    public BackgroundMode BackgroundMode { get; set; } = BackgroundMode.AutoGradient;
    public string BackgroundColor1Hex { get; set; } = "#303137";
    public string BackgroundColor2Hex { get; set; } = "#4B4C53";
    public BackgroundPattern BackgroundPattern { get; set; } = BackgroundPattern.SoftGrain;
    public int AutoGradientLightenPercent { get; set; } = 20;

    public AppSettings Copy() => new()
    {
        OutputWidth = OutputWidth,
        PaddingPercent = PaddingPercent,
        ShadowStrength = ShadowStrength,
        CanvasRatio = CanvasRatio,
        Layout = Layout,
        BackgroundMode = BackgroundMode,
        BackgroundColor1Hex = BackgroundColor1Hex,
        BackgroundColor2Hex = BackgroundColor2Hex,
        BackgroundPattern = BackgroundPattern,
        AutoGradientLightenPercent = AutoGradientLightenPercent
    };

    public bool IsValid() =>
        OutputWidth is >= 640 and <= 4096 &&
        PaddingPercent is >= 0 and <= 25 &&
        double.IsFinite(ShadowStrength) && ShadowStrength is >= 0 and <= 0.4 &&
        Enum.IsDefined(CanvasRatio) &&
        Enum.IsDefined(Layout) &&
        Enum.IsDefined(BackgroundMode) &&
        IsValidColorHex(BackgroundColor1Hex) &&
        IsValidColorHex(BackgroundColor2Hex) &&
        Enum.IsDefined(BackgroundPattern) &&
        AutoGradientLightenPercent is >= 0 and <= 100;

    private static bool IsValidColorHex(string? value) =>
        value is { Length: 7 } && value[0] == '#' &&
        uint.TryParse(value.AsSpan(1), NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out _);
}

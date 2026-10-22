using Avalonia;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Xunit;

namespace Softstage.Mac.Tests;

public sealed class FontTests
{
    private const string GoogleSansFlex = "Google Sans Flex";

    private static FontFamily AppFont =>
        (FontFamily)Application.Current!.Resources["AppFont"]!;

    [AvaloniaFact]
    public void GoogleSansFlex_RegularResolvesToItsFamilyName()
    {
        var typeface = new Typeface(AppFont, FontStyle.Normal, FontWeight.Regular);

        Assert.True(FontManager.Current.TryGetGlyphTypeface(typeface, out var glyph), "The embedded regular face did not resolve.");
        Assert.Equal(GoogleSansFlex, glyph.FamilyName);
        Assert.Equal(FontWeight.Regular, glyph.Weight);
    }

    [AvaloniaFact]
    public void GoogleSansFlex_BoldResolvesToItsFamilyName()
    {
        var typeface = new Typeface(AppFont, FontStyle.Normal, FontWeight.Bold);

        Assert.True(FontManager.Current.TryGetGlyphTypeface(typeface, out var glyph), "The embedded bold face did not resolve.");
        Assert.Equal(GoogleSansFlex, glyph.FamilyName);
        Assert.Equal(FontWeight.Bold, glyph.Weight);
    }

    [AvaloniaFact]
    public void BogusFontName_DoesNotResolveToGoogleSansFlex()
    {
        var bogus = new FontFamily("avares://Softstage/Assets/Fonts#Not A Real Font Family");
        var typeface = new Typeface(bogus, FontStyle.Normal, FontWeight.Regular);

        // A typo in a family name must not resolve quietly. Otherwise the UI would fall back without any signal.
        Assert.False(FontManager.Current.TryGetGlyphTypeface(typeface, out _),
            "A family name that does not exist in the embedded fonts resolved to a glyph typeface.");
    }
}

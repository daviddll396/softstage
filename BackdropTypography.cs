using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;

namespace Backdrop;

internal static class BackdropTypography
{
    internal const string FamilyName = "Google Sans Flex";
    private const string RegularResourceName = "Backdrop.Fonts.GoogleSansFlex-Regular.ttf";
    private const string BoldResourceName = "Backdrop.Fonts.GoogleSansFlex-Bold.ttf";
    private const string LicenseResourceName = "Backdrop.Fonts.OFL.txt";
    private static readonly object Sync = new();
    private static readonly PrivateFontCollection PrivateFonts = new();
    private static readonly List<GCHandle> PinnedFontData = [];
    private static readonly List<IntPtr> GdiFontResources = [];
    private static FontFamily? _family;

    internal static void Initialize()
    {
        if (_family is not null)
            return;

        lock (Sync)
        {
            if (_family is not null)
                return;

            RegisterFont(RegularResourceName);
            RegisterFont(BoldResourceName);
            _family = PrivateFonts.Families.FirstOrDefault(family => family.Name == FamilyName)
                ?? throw new InvalidOperationException($"The embedded {FamilyName} font family could not be loaded.");
            if (!_family.IsStyleAvailable(FontStyle.Regular) || !_family.IsStyleAvailable(FontStyle.Bold))
                throw new InvalidOperationException($"The embedded {FamilyName} regular and bold font faces are not both available.");
        }
    }

    internal static Font Create(float size, FontStyle style = FontStyle.Regular, GraphicsUnit unit = GraphicsUnit.Point)
    {
        Initialize();
        return new Font(_family!, size, style, unit);
    }

    internal static bool HasEmbeddedLicense
    {
        get
        {
            using var stream = typeof(BackdropTypography).Assembly.GetManifestResourceStream(LicenseResourceName);
            if (stream is null)
                return false;
            using var reader = new StreamReader(stream);
            var license = reader.ReadToEnd();
            return license.Contains("SIL OPEN FONT LICENSE Version 1.1", StringComparison.Ordinal);
        }
    }

    internal static (string GdiPlusRegular, string GdiPlusBold, string GdiRegular, string GdiBold) GetBackendFamilyNames()
    {
        Initialize();
        using var regular = Create(9.5F);
        using var bold = Create(9.5F, FontStyle.Bold);
        return (regular.FontFamily.Name, bold.FontFamily.Name,
            GetGdiTextFaceName(regular), GetGdiTextFaceName(bold));
    }

    private static void RegisterFont(string resourceName)
    {
        using var stream = typeof(BackdropTypography).Assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"The embedded font resource '{resourceName}' is missing.");
        var fontData = new byte[checked((int)stream.Length)];
        stream.ReadExactly(fontData);

        var pinnedData = GCHandle.Alloc(fontData, GCHandleType.Pinned);
        PinnedFontData.Add(pinnedData);
        PrivateFonts.AddMemoryFont(pinnedData.AddrOfPinnedObject(), fontData.Length);

        var gdiResource = AddFontMemResourceEx(pinnedData.AddrOfPinnedObject(), (uint)fontData.Length, IntPtr.Zero, out var fontsAdded);
        if (gdiResource == IntPtr.Zero || fontsAdded == 0)
            throw new InvalidOperationException($"The embedded font resource '{resourceName}' could not be registered for GDI text rendering.");
        GdiFontResources.Add(gdiResource);
    }

    private static string GetGdiTextFaceName(Font font)
    {
        var dc = CreateCompatibleDC(IntPtr.Zero);
        if (dc == IntPtr.Zero)
            throw new InvalidOperationException("A GDI device context could not be created for the font check.");

        var hfont = font.ToHfont();
        var previous = SelectObject(dc, hfont);
        try
        {
            if (previous == IntPtr.Zero || previous == new IntPtr(-1))
                throw new InvalidOperationException("The embedded font could not be selected for GDI text rendering.");

            var faceName = new StringBuilder(128);
            if (GetTextFaceW(dc, faceName.Capacity, faceName) <= 0)
                throw new InvalidOperationException("GDI did not report the selected text face name.");
            return faceName.ToString();
        }
        finally
        {
            if (previous != IntPtr.Zero && previous != new IntPtr(-1))
                _ = SelectObject(dc, previous);
            _ = DeleteObject(hfont);
            _ = DeleteDC(dc);
        }
    }

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr AddFontMemResourceEx(IntPtr font, uint fontSize, IntPtr reserved, out uint fontsAdded);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode, EntryPoint = "GetTextFaceW", SetLastError = true)]
    private static extern int GetTextFaceW(IntPtr hdc, int count, StringBuilder faceName);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr value);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr value);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);
}

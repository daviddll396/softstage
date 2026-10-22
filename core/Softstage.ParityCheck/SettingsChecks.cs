using System.Text.Json;
using Softstage.Core;

namespace Softstage.ParityCheck;

/// <summary>
/// Load and Save round trip with the Windows settings.json shape. The user's file is copied to a temp
/// folder first and only the copy is read. A synthetic file with non-default enum values always runs.
/// </summary>
internal static class SettingsChecks
{
    private const string WindowsShapeJson = """
{
  "OutputWidth": 2560,
  "PaddingPercent": 12,
  "ShadowStrength": 0.25,
  "CanvasRatio": "square1x1",
  "Layout": "grid",
  "BackgroundMode": "pattern",
  "BackgroundColor1Hex": "#112233",
  "BackgroundColor2Hex": "#AABBCC",
  "BackgroundPattern": "dots",
  "AutoGradientLightenPercent": 35
}
""";

    public static bool Run()
    {
        Console.WriteLine("== Settings: Load/Save round trip ==");
        var workDirectory = Path.Combine(Path.GetTempPath(), "softstage-core-check-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workDirectory);
        try
        {
            var ok = true;

            var missing = SettingsStore.Load(Path.Combine(workDirectory, "missing.json"));
            ok &= Check("missing file gives defaults and no warning", missing.Warning is null && Same(missing.Settings, new AppSettings()));

            var userFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Backdrop", "settings.json");
            if (File.Exists(userFile))
            {
                var copy = Path.Combine(workDirectory, "windows-user-settings.json");
                File.Copy(userFile, copy);
                ok &= RoundTrip("user settings file (Windows app, read from a copy)", copy, workDirectory);
            }
            else
            {
                Console.WriteLine("  user settings file not found; skipped");
            }

            var synthetic = Path.Combine(workDirectory, "windows-shape.json");
            File.WriteAllText(synthetic, WindowsShapeJson);
            ok &= RoundTrip("Windows-shape file with non-default enum values", synthetic, workDirectory);
            var read = SettingsStore.Load(synthetic).Settings;
            ok &= Check("Windows-shape values read as written",
                read.OutputWidth == 2560 && read.PaddingPercent == 12 && read.ShadowStrength == 0.25 &&
                read.CanvasRatio == CanvasRatio.Square1x1 && read.Layout == CompositionLayout.Grid &&
                read.BackgroundMode == BackgroundMode.Pattern && read.BackgroundPattern == BackgroundPattern.Dots &&
                read.BackgroundColor1Hex == "#112233" && read.BackgroundColor2Hex == "#AABBCC" &&
                read.AutoGradientLightenPercent == 35);

            var corruptPath = Path.Combine(workDirectory, "corrupt.json");
            File.WriteAllText(corruptPath, "{ not json");
            var corrupt = SettingsStore.Load(corruptPath);
            ok &= Check("corrupt file gives defaults and the Windows warning text",
                corrupt.Warning == "Softstage could not read its saved preferences. It will use the defaults until you save new preferences." &&
                Same(corrupt.Settings, new AppSettings()));

            var outOfRangePath = Path.Combine(workDirectory, "out-of-range.json");
            File.WriteAllText(outOfRangePath, "{ \"OutputWidth\": 100 }");
            ok &= Check("out-of-range value is treated as corrupt", SettingsStore.Load(outOfRangePath).Warning is not null);

            var nested = Path.Combine(workDirectory, "new", "folder", "settings.json");
            SettingsStore.Save(new AppSettings { OutputWidth = 3840 }, nested);
            var folderFiles = Directory.GetFiles(Path.GetDirectoryName(nested)!);
            ok &= Check("save creates the folder and leaves no temporary file",
                folderFiles.Length == 1 && Path.GetFileName(folderFiles[0]) == "settings.json");

            var invalidMessage = string.Empty;
            try
            {
                SettingsStore.Save(new AppSettings { OutputWidth = 100 }, Path.Combine(workDirectory, "invalid.json"));
            }
            catch (SoftstageException ex)
            {
                invalidMessage = ex.Message;
            }
            ok &= Check("saving invalid settings throws the user-facing message",
                invalidMessage == "Settings are outside the supported range." && !File.Exists(Path.Combine(workDirectory, "invalid.json")));

            return ok;
        }
        finally
        {
            try { Directory.Delete(workDirectory, recursive: true); } catch { }
        }
    }

    private static bool RoundTrip(string label, string sourcePath, string workDirectory)
    {
        var originalText = File.ReadAllText(sourcePath);
        var first = SettingsStore.Load(sourcePath);
        var savedFolder = Path.Combine(workDirectory, "saved", Guid.NewGuid().ToString("N"));
        var savedPath = Path.Combine(savedFolder, "settings.json");
        SettingsStore.Save(first.Settings, savedPath);
        var second = SettingsStore.Load(savedPath);
        var savedText = File.ReadAllText(savedPath);

        var valuesOk = first.Warning is null && second.Warning is null && Same(first.Settings, second.Settings);
        var jsonOk = CompareJson(originalText, savedText, out var detail);
        var leftovers = Directory.GetFiles(savedFolder).Count(file => Path.GetFileName(file) != "settings.json");

        Console.WriteLine($"  values: {Describe(second.Settings)}");
        Console.WriteLine($"  json: {detail}");
        return Check($"round trip: {label}", valuesOk && jsonOk && leftovers == 0);
    }

    private static bool Same(AppSettings a, AppSettings b) =>
        a.OutputWidth == b.OutputWidth &&
        a.PaddingPercent == b.PaddingPercent &&
        a.ShadowStrength == b.ShadowStrength &&
        a.CanvasRatio == b.CanvasRatio &&
        a.Layout == b.Layout &&
        a.BackgroundMode == b.BackgroundMode &&
        a.BackgroundColor1Hex == b.BackgroundColor1Hex &&
        a.BackgroundColor2Hex == b.BackgroundColor2Hex &&
        a.BackgroundPattern == b.BackgroundPattern &&
        a.AutoGradientLightenPercent == b.AutoGradientLightenPercent;

    /// <summary>Compares property names and values of the original and saved JSON, ignoring order and whitespace.</summary>
    private static bool CompareJson(string original, string saved, out string detail)
    {
        using var a = JsonDocument.Parse(original);
        using var b = JsonDocument.Parse(saved);
        var problems = new List<string>();

        var namesA = a.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var namesB = b.RootElement.EnumerateObject().Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        if (!namesA.SequenceEqual(namesB))
            problems.Add("property names differ");

        foreach (var property in a.RootElement.EnumerateObject())
        {
            if (!b.RootElement.TryGetProperty(property.Name, out var other))
            {
                problems.Add($"{property.Name} missing after save");
                continue;
            }

            var same = property.Value.ValueKind switch
            {
                JsonValueKind.String => other.ValueKind == JsonValueKind.String && property.Value.GetString() == other.GetString(),
                JsonValueKind.Number => other.ValueKind == JsonValueKind.Number && property.Value.GetDouble() == other.GetDouble(),
                _ => property.Value.GetRawText() == other.GetRawText()
            };
            if (!same)
                problems.Add($"{property.Name}: {property.Value.GetRawText()} became {other.GetRawText()}");
        }

        detail = problems.Count == 0
            ? $"{namesA.Length} property names and values match (PascalCase keys, camelCase enum strings)"
            : string.Join("; ", problems);
        return problems.Count == 0;
    }

    private static string Describe(AppSettings s) =>
        $"OutputWidth={s.OutputWidth}, PaddingPercent={s.PaddingPercent}, ShadowStrength={s.ShadowStrength}, " +
        $"CanvasRatio={s.CanvasRatio}, Layout={s.Layout}, BackgroundMode={s.BackgroundMode}, " +
        $"BackgroundColor1Hex={s.BackgroundColor1Hex}, BackgroundColor2Hex={s.BackgroundColor2Hex}, " +
        $"BackgroundPattern={s.BackgroundPattern}, AutoGradientLightenPercent={s.AutoGradientLightenPercent}";

    private static bool Check(string description, bool pass)
    {
        Console.WriteLine($"[{(pass ? "PASS" : "FAIL")}] {description}");
        return pass;
    }
}

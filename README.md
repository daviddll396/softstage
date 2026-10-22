# Softstage

Softstage combines up to nine pictures into one PNG. The window shows a live preview. Add files with **Add images** or drop them into the window. Select an image and use the previous or next button to set its order. You can also press **Alt+Up** and **Alt+Down**. Pictures keep their original shape and are never cropped.

Image processing runs on your device. Softstage does not upload pictures, require an account, or include telemetry or analytics.

## Download

**Windows**

- **Where:** Microsoft Store
- **Status:** Microsoft Store: coming soon. The listing is not live yet.
- **Guidance:** Install from the Microsoft Store once it is listed. There is no download link yet.

**Mac**

- **Where:** Download the `.dmg` from [GitHub Releases](https://github.com/daviddll396/softstage/releases/latest). It is not on the Mac App Store. Requires macOS 13.0 or later.
- **Status:** New build, not yet tested on macOS. It is unsigned and not notarized by Apple, so macOS blocks the first launch.
- **Guidance:** Open the DMG, drag Softstage to Applications, then right-click it, choose Open, and click Open in the dialog. If macOS still blocks it, run `xattr -dr com.apple.quarantine /Applications/Softstage.app` in Terminal. If it fails, report it in GitHub Issues for this repository. Full steps are in [Install Softstage for Mac](mac/packaging/INSTALL.md).

## License and distribution

The native Windows app source files listed in the repository [LICENSE](LICENSE) are licensed under MIT. The website, React Bits components, libraries, fonts, sample artwork, logos, and other assets are outside that grant unless their own notices say otherwise. See [third-party notices](docs/legal/third-party-notices.md).

Softstage is planned as a free app. Windows users will get it from the Microsoft Store, which is not live yet. Mac users download the DMG from GitHub Releases. See the [release checklist](docs/release-checklist.md) for remaining release steps.

## Website, privacy, and support

The website is maintained in the separate sibling project `../softstage-site` and the private `softstage-site` repository. Its Vercel project root is that repository root. The planned production domain is https://softstage.tolani.xyz; it has not been deployed.

The Privacy page is finalized at https://softstage.tolani.xyz/privacy. Production response headers, cookie behavior, and hosting log scope still need verification before publication. For support or privacy requests, email [fagbenjatolani@gmail.com](mailto:fagbenjatolani@gmail.com).

## Composition

- **Canvas ratio:** Auto, Wide 16:9, Square 1:1, or Portrait 4:5.
- **Layout:** Auto, Row, or Grid. Auto places two to four portrait pictures in one row. Other groups use a grid. Grid centers its final row when it is not full.
- **Auto ratio:** A single picture uses its own ratio. A row uses the natural ratio of its pictures, limited to 0.5:1 through 3:1. A grid uses the average picture ratio and its row and column count.
- **Long edge:** The output's longest side, from 640 to 4096 pixels. The 16:9 default at 1920 pixels makes a 1920 × 1080 PNG.
- **Padding:** A percentage applied to each canvas edge.
- **Shadow:** A soft Gaussian shadow under each picture.
- **Background:** Choose **Automatic gradient**, **Solid color**, **Two-color gradient**, or **Pattern**. Automatic gradient samples every selected picture on an equal 24 × 24 grid. It keeps gray colors for monochrome pictures. Set **Lighten colors (%)** from 0 for the sampled colors to 100 for white. The default is 20. This setting only affects Automatic gradient. Pattern uses one base color with soft grain or spaced dots.

The live preview uses a 1000-pixel long edge. Softstage checks the full source size and orientation before it reduces a picture. It reuses the reduced picture while the source stays unchanged. The final PNG uses the selected long edge. A small spinner shows while the preview updates after a short pause. **Save preferences** stores settings in `%LOCALAPPDATA%\Backdrop\settings.json`; changes are not saved automatically.

Choose **Background** above the preview to edit the background. Use a quick color swatch, enter an opaque `#RRGGBB` value, or open the custom advanced color picker. Invalid visible color text keeps the editor open until you correct it. Background changes update the preview. **Apply** keeps them in the current session; choose **Save preferences** in the main window to keep them for later.

The advanced picker has hue and saturation/brightness controls. It shows the current and new colors. Canvas size, padding, shadow, and lightening use horizontal number selectors. Click a left or right chevron, or type a whole number in the center.

Softstage embeds regular and bold Google Sans Flex text faces for offline use. The faces are derived from the official v4.007 variable font at optical size 12 and width 100. The app also embeds the SIL Open Font License. See `assets/fonts/OFL.txt` and `docs/design.md` for the source and axis settings.

Softstage preserves the source files and never replaces an output. A single picture is named `<image>-softstage.png`. A group is named `softstage-composition.png`. If that name exists, Softstage adds a number.

## Run

Use the .NET 10 SDK on Windows:

```powershell
dotnet run --project Backdrop.csproj
```

Publish a self-contained x64 application:

```powershell
dotnet publish Backdrop.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/v2
.\scripts\build-shell.ps1
```

The project has no application package references. Self-contained publishing restores the .NET runtime pack.

To make a PNG from a command prompt or a shortcut, run:

```powershell
Backdrop.exe --generate "C:\Photos\one.jpg" "C:\Photos\two.jpg"
```

The command prints the output path when it has a console. When Explorer starts it, a small window appears for up to eight seconds. It does not take focus. Choose **Open image** or **Show folder** to open the result.

## Windows installer candidate

Windows users will get Softstage from the Microsoft Store, so this installer is a candidate for testing, not a public download. It targets Windows 11 on x64 hardware. The installer includes the .NET runtime. Users do not need the SDK, administrator access, Developer Mode, or a test certificate. It installs for the current user in `%LOCALAPPDATA%\Programs\Backdrop`.

The installer registers **Create composition with Softstage** for Explorer's **Show more options** menu. Actual menu visibility still needs the manual Explorer check. The command sends the selected pictures to one composition. Use **Remove Explorer menu** in the app to disable it, or **Enable Explorer menu** to enable it again. The consumer installer uses a classic shell command. The older sparse package scripts are for development only.

The installer candidate is unsigned. Windows can show a SmartScreen warning. Publication is pending the clean-machine and real Explorer checks in [the release checklist](docs/release-checklist.md). Microsoft Store packaging and certification remain separate steps.

### Build the installer

The build requires .NET SDK **10.0.401**, Inno Setup **6.7.3**, and the Visual Studio C++ build tools with a Windows SDK. Put the portable .NET SDK in `artifacts/tools/dotnet`. Put the Inno Setup compiler in `artifacts/tools/InnoSetupPortable/tools/ISCC.exe`. Use the official [Microsoft SDK download](https://dotnet.microsoft.com/en-us/download/dotnet/10.0) and the pinned [Inno Setup tools package](https://www.nuget.org/packages/Tools.InnoSetup/6.7.3). The build checks the compiler's signature.

```powershell
.\scripts\build-release.ps1 -Version 0.1.0
```

The script publishes a self-contained app, builds the native shell command, runs the staged app checks and native harness, then builds the installer. The installer and SHA-256 file go in `artifacts/release-output`. Generated files are not committed.

Use **Remove**, **Clear**, or **Delete** while the image list has focus to remove pictures from the composition. These actions do not delete source files. During an export, **Create PNG** changes to **Cancel**. Cancellation is checked between processing steps. It does not interrupt an active image decode or encode. A completed export remains valid if cancellation arrives after the file was committed.

## Inputs and checks

Supported formats are PNG, JPEG, BMP, GIF, and TIFF. Each picture can be at most 40 megapixels. A selection can be at most 80 megapixels total. At 32 bits per pixel, 80 megapixels use about 320 MB for one pixel buffer, before decoder and output working data.

Run the built-in checks and create wide and compact form captures:

```powershell
dotnet run --project Backdrop.csproj -- --self-check artifacts/self-check
```

The check covers canvas ratios, image order, row and grid placement, background modes and patterns, legacy and invalid preferences, output names, source preservation, cancellation, and 960 × 720 and 800 × 600 form captures. It does not invoke Explorer or confirm that the context menu appears.

# Microsoft Store package preparation

## Package identity

The prepared package uses this identity:

| Field | Value |
| --- | --- |
| Package name | `TolaniFagbenja.Softstage` |
| Store display name | `Softstage` |
| Publisher | `CN=6E891E62-87B5-4E29-AB6B-9C47CEB67CAB` |
| Publisher display name | `Tolani Fagbenja` |
| Version | `1.0.0.0` |
| Architecture | x64 |
| Device family | Windows.Desktop |
| Minimum Windows version | 10.0.22000.0 |

The package includes the app and `Backdrop.Shell.dll`. The manifest registers the DLL as a COM server and adds the image commands to File Explorer. The DLL and `Backdrop.exe` stay beside each other in the package.

## Build the package

Run this command from the Softstage app repository:

```powershell
.\scripts\build-store.ps1
```

The script requires the portable .NET SDK 10.0.401 in `artifacts/tools/dotnet`, the Windows SDK 10.0.26100.0 or later, and the Visual Studio x64 C++ tools.

The script publishes a Release, self-contained, single-file x64 app. It then builds the native File Explorer command DLL. The Store build excludes the app's developer controls for enabling or removing the Explorer command. It also skips the PowerShell status check at startup. The existing classic build keeps those controls and checks.

The package stage contains only the app, the shell DLL, the manifest, three Softstage logos, privacy and third-party notices, and the app and font licenses. MakeAppx creates the MSIX with semantic validation enabled. The build does not use the `/nv` option.

The script writes these outputs:

- `artifacts/store-output/Softstage-1.0.0.0-x64.msix`
- `artifacts/store-output/Softstage-1.0.0.0-x64.msix.sha256`
- `artifacts/store-output/Softstage-1.0.0.0-x64.report.txt`
- A fresh `artifacts/store-stage-*` directory with the package contents.

The current candidate SHA-256 is `0c9e8e819fb8801b97cd7da7caa9e7a177db22b4910f9e4cdb9be850bf1418fb`. Use the checksum sidecar to check the package file.

For a new package version, update both the manifest's `Identity` version and `$packageVersion` in `scripts/build-store.ps1`.

## Checks and limits

The Store publish completed with .NET SDK 10.0.401. MSVC built the app's Explorer DLL. MakeAppx created the MSIX with semantic validation enabled. MakeAppx then unpacked the package. The unpacked manifest identity, minimum Windows version, logos, legal files, app, and DLL were checked. The package contains no harness, object, library, or debug files.

This is a package candidate only. It has not passed Store certification and is not published. The package is not signed. No signing certificate was installed. The app was not installed or launched. The package was not uploaded to Partner Center. No Explorer restart or system registration was run. No Windows App Certification Kit, clean Windows VM, upgrade, or coexistence check was run. No app tests were run.

MakeAppx creates an `.msix`, not an `.msixupload`. Partner Center accepts `.msix` packages, and Microsoft recommends `.msixupload` for Windows 10 and later. See [Upload MSIX app packages](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/upload-app-packages) and [Create an MSIX package with MakeAppx](https://learn.microsoft.com/en-us/windows/msix/package/create-app-package-with-makeappx-tool) for the current upload steps and package options.

## Next Partner Center steps

1. Confirm the developer account and reserve the Store name `Softstage`. Check that the reserved product identity matches the package name and publisher above. See [Reserve an MSIX app name](https://learn.microsoft.com/en-us/windows/apps/publish/publish-your-app/msix/reserve-your-apps-name).
2. Create the MSIX app submission. Add the Store listing, privacy details, age rating, availability, and pricing.
3. Upload the package. Review Partner Center's package and policy results. Complete its certification steps before publishing.
4. If Partner Center requires an `.msixupload`, create that upload file with the supported packaging workflow. Do not change the package identity to work around a validation error.

The Store package uses the packaged-app File Explorer model. See Microsoft's [File Explorer context menu guide](https://learn.microsoft.com/en-us/windows/apps/desktop/modernize/integrate-packaged-app-with-file-explorer) for the package registration model.

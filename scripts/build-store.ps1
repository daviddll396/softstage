[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'artifacts'))
$dotnet = Join-Path $artifactsRoot 'tools\dotnet\dotnet.exe'
$project = Join-Path $projectRoot 'Backdrop.csproj'
$shellBuildScript = Join-Path $PSScriptRoot 'build-shell.ps1'
$manifestSource = Join-Path $projectRoot 'installer\store\AppxManifest.xml'
$appIcon = Join-Path $projectRoot 'assets\app-icon.png'
$packageVersion = '1.0.1.0'
$packageName = "Softstage-$packageVersion-x64"
$buildId = [Guid]::NewGuid().ToString('N')
$stamp = [DateTime]::UtcNow.ToString('yyyyMMdd-HHmmss')
$stageRoot = Join-Path $artifactsRoot "store-stage-$packageVersion-$stamp-$buildId"
$outputRoot = Join-Path $artifactsRoot 'store-output'
$packagePath = Join-Path $outputRoot "$packageName.msix"
$packageTemp = Join-Path $outputRoot ".$packageName-$buildId.msix"
$publishRoot = Join-Path ([IO.Path]::GetTempPath()) "SoftstageStorePublish-$buildId"

foreach ($path in @($dotnet, $project, $shellBuildScript, $manifestSource, $appIcon)) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "A Store build input is missing: $path"
    }
}
if ((Test-Path -LiteralPath $stageRoot) -or (Test-Path -LiteralPath $publishRoot)) {
    throw 'A generated Store build path already exists. Start the build again to use a new path.'
}

$sdkVersion = (& $dotnet --version).Trim()
if ($LASTEXITCODE -ne 0 -or $sdkVersion -ne '10.0.401') {
    throw "The Store build requires portable .NET SDK 10.0.401. Found '$sdkVersion'."
}

$windowsSdkRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
$windowsSdk = Get-ChildItem -LiteralPath $windowsSdkRoot -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^10\.0\.\d+(\.\d+)?$' -and [version]$_.Name -ge [version]'10.0.26100.0' } |
    Sort-Object { [version]$_.Name } -Descending |
    ForEach-Object { Join-Path $_.FullName 'x64' } |
    Where-Object { Test-Path -LiteralPath (Join-Path $_ 'makeappx.exe') -PathType Leaf } |
    Select-Object -First 1
if (-not $windowsSdk) {
    throw 'The Store build requires Windows SDK 10.0.26100.0 or later with x64 MakeAppx.'
}
$makeAppx = Join-Path $windowsSdk 'makeappx.exe'
$windowsSdkVersion = Split-Path (Split-Path $windowsSdk -Parent) -Leaf

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
New-Item -ItemType Directory -Path $stageRoot | Out-Null
New-Item -ItemType Directory -Path $publishRoot | Out-Null

& $dotnet publish $project -c Release -f net10.0-windows -r win-x64 --self-contained true `
    -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:SoftstageStoreBuild=true -p:Version=$packageVersion -p:FileVersion=$packageVersion `
    -p:AssemblyVersion=$packageVersion -o $publishRoot
if ($LASTEXITCODE -ne 0) {
    throw "The self-contained Store app publish failed with exit code $LASTEXITCODE."
}

$appExecutable = Join-Path $publishRoot 'Backdrop.exe'
if (-not (Test-Path -LiteralPath $appExecutable -PathType Leaf)) {
    throw 'The self-contained Store app publish did not produce Backdrop.exe.'
}
Copy-Item -LiteralPath $appExecutable -Destination (Join-Path $stageRoot 'Backdrop.exe')

& $shellBuildScript -OutputRoot $stageRoot -SkipSparsePackage
if ($LASTEXITCODE -ne 0) {
    throw "The native Explorer command build failed with exit code $LASTEXITCODE."
}

$shellDll = Join-Path $stageRoot 'Backdrop.Shell.dll'
if (-not (Test-Path -LiteralPath $shellDll -PathType Leaf)) {
    throw 'The native Explorer command DLL is missing from the Store stage.'
}
foreach ($intermediate in @('BackdropShell.obj', 'Backdrop.Shell.lib', 'Backdrop.Shell.exp', 'BackdropShellHarness.exe', 'BackdropShellHarness.obj')) {
    Remove-Item -LiteralPath (Join-Path $stageRoot $intermediate) -Force -ErrorAction SilentlyContinue
}

$assetsRoot = Join-Path $stageRoot 'Assets'
$licensesRoot = Join-Path $stageRoot 'licenses'
$legalRoot = Join-Path $stageRoot 'legal'
New-Item -ItemType Directory -Path $assetsRoot, $licensesRoot, $legalRoot -Force | Out-Null

Add-Type -AssemblyName System.Drawing
$iconSource = [System.Drawing.Image]::FromFile($appIcon)
try {
    foreach ($logo in @(
        [pscustomobject]@{ Name = 'StoreLogo.png'; Size = 50 },
        [pscustomobject]@{ Name = 'Logo44.png'; Size = 44 },
        [pscustomobject]@{ Name = 'Logo150.png'; Size = 150 }
    )) {
        $bitmap = [System.Drawing.Bitmap]::new($logo.Size, $logo.Size, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $graphics = [System.Drawing.Graphics]::FromImage($bitmap)
        try {
            $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
            $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
            $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
            $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
            $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
            $graphics.Clear([System.Drawing.Color]::Transparent)
            $graphics.DrawImage(
                $iconSource,
                [System.Drawing.Rectangle]::new(0, 0, $logo.Size, $logo.Size),
                0,
                0,
                $iconSource.Width,
                $iconSource.Height,
                [System.Drawing.GraphicsUnit]::Pixel)
            $bitmap.Save((Join-Path $assetsRoot $logo.Name), [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $graphics.Dispose()
            $bitmap.Dispose()
        }
    }
}
finally {
    $iconSource.Dispose()
}

Copy-Item -LiteralPath $manifestSource -Destination (Join-Path $stageRoot 'AppxManifest.xml')
Copy-Item -LiteralPath (Join-Path $projectRoot 'LICENSE') -Destination (Join-Path $licensesRoot 'Softstage-MIT-LICENSE.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'assets\fonts\OFL.txt') -Destination (Join-Path $licensesRoot 'GoogleSansFlex-OFL.txt')
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\legal\third-party-notices.md') -Destination (Join-Path $legalRoot 'third-party-notices.md')
Copy-Item -LiteralPath (Join-Path $projectRoot 'docs\legal\privacy.md') -Destination (Join-Path $legalRoot 'privacy.md')

$expectedFiles = @(
    'AppxManifest.xml',
    'Backdrop.exe',
    'Backdrop.Shell.dll',
    'Assets\StoreLogo.png',
    'Assets\Logo44.png',
    'Assets\Logo150.png',
    'licenses\Softstage-MIT-LICENSE.txt',
    'licenses\GoogleSansFlex-OFL.txt',
    'legal\third-party-notices.md',
    'legal\privacy.md'
)
$actualFiles = @(Get-ChildItem -LiteralPath $stageRoot -File -Recurse | ForEach-Object {
    $_.FullName.Substring($stageRoot.TrimEnd('\').Length + 1).Replace('/', '\')
} | Sort-Object)
$sortedExpectedFiles = @($expectedFiles | Sort-Object)
if (($actualFiles -join "`n") -ne ($sortedExpectedFiles -join "`n")) {
    throw "The Store stage contains unexpected or missing files. Actual: $($actualFiles -join ', ')"
}

& $makeAppx pack /d $stageRoot /p $packageTemp /o
if ($LASTEXITCODE -ne 0 -or -not (Test-Path -LiteralPath $packageTemp -PathType Leaf)) {
    throw "MakeAppx failed to create the Store package (exit code $LASTEXITCODE)."
}
Move-Item -LiteralPath $packageTemp -Destination $packagePath -Force

$hash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumPath = "$packagePath.sha256"
Set-Content -LiteralPath $checksumPath -Value "$hash *$([IO.Path]::GetFileName($packagePath))" -Encoding Ascii

$reportPath = Join-Path $outputRoot "$packageName.report.txt"
$report = @(
    'Softstage Microsoft Store package preparation',
    "Created UTC: $([DateTime]::UtcNow.ToString('yyyy-MM-dd HH:mm:ss'))",
    "Package: $packagePath",
    "Staging directory: $stageRoot",
    'Identity: TolaniFagbenja.Softstage',
    'Publisher: CN=6E891E62-87B5-4E29-AB6B-9C47CEB67CAB',
    'Publisher display name: Tolani Fagbenja',
    "Version: $packageVersion",
    'Architecture: x64',
    'Target: Windows.Desktop, minimum 10.0.22000.0',
    "Portable .NET SDK: $sdkVersion",
    "Windows SDK: $windowsSdkVersion",
    'Publish: Release, self-contained, single-file; SOFTSTAGE_STORE defined.',
    'Package creation: MakeAppx pack with semantic validation enabled; /nv was not used.',
    "SHA-256: $hash",
    'Local package signing: not performed.',
    'App installation, system registration, Explorer restart, and Partner Center upload: not performed.',
    'The native shell harness was compiled by the existing build script but was not run.'
)
Set-Content -LiteralPath $reportPath -Value $report -Encoding UTF8

$tempRoot = [IO.Path]::GetFullPath([IO.Path]::GetTempPath()).TrimEnd('\') + '\'
$fullPublishRoot = [IO.Path]::GetFullPath($publishRoot)
if (-not $fullPublishRoot.StartsWith($tempRoot, [StringComparison]::OrdinalIgnoreCase) -or
    -not [IO.Path]::GetFileName($fullPublishRoot).StartsWith('SoftstageStorePublish-', [StringComparison]::Ordinal)) {
    throw "The temporary publish output is outside its expected generated folder: $fullPublishRoot"
}
Remove-Item -LiteralPath $fullPublishRoot -Recurse -Force

Write-Output 'Softstage Store MSIX package created.'
Write-Output "Identity: TolaniFagbenja.Softstage, version $packageVersion, x64"
Write-Output "Package: $packagePath"
Write-Output "SHA-256: $checksumPath"
Write-Output "Build report: $reportPath"
Write-Output "Package stage: $stageRoot"

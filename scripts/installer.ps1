# Publishes Dev Monitor as a self-contained win-x64 build and packages it with Inno Setup.
# The setup ends up in build\installer\DevMonitor-<version>-setup.exe.
param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent

[xml]$props = Get-Content (Join-Path $root "version.props")
$version = $props.Project.PropertyGroup.Version
if (-not $version) { throw "version.props does not define a Version." }

$iscc = Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source
if (-not $iscc) {
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $iscc) { throw "Inno Setup 6 was not found. Install it with: winget install JRSoftware.InnoSetup" }

$publish = Join-Path $root "build\publish\installer"
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }

Write-Host "Publishing Dev Monitor $version ($Configuration)"
dotnet publish (Join-Path $root "src\DevMonitor.App\DevMonitor.App.csproj") `
    -c $Configuration -r win-x64 --self-contained `
    -p:DebugType=None -p:DebugSymbols=false `
    -o $publish
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Write-Host "Building the installer"
& $iscc /Q "/DAppVersion=$version" "/DSourceDir=$publish" (Join-Path $root "installer\DevMonitor.iss")
if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed." }

$setup = Join-Path $root "build\installer\DevMonitor-$version-setup.exe"
Write-Host "Installer: $setup"

# DSP-RecipeSlots build script
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1                 # dev build (test content + debug channel)
#   powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -Release        # release build (no test content / no debug channel)
#   powershell -NoProfile -ExecutionPolicy Bypass -File build.ps1 -Release -Package   # release + r2modman/Thunderstore zip
#   ... -Deploy        copy the built dll into the game's BepInEx/plugins/DSP-RecipeSlots
#
# NOTE: keep this file ASCII-only. Windows PowerShell 5.1 reads BOM-less files as ANSI,
#       so non-ASCII comments can corrupt parsing when invoked with -File.
param(
    [string]$GameDir = 'C:\Games\Steam\steamapps\common\Dyson Sphere Program',
    [string]$DotnetExe = 'C:\WorkWork\DSP MOD\DSP-MPD-runtime-debug\tools\dotnet-sdk\dotnet.exe',
    [switch]$Release,
    [switch]$Package,
    [switch]$Deploy
)

$ErrorActionPreference = 'Stop'

$scriptPath = $MyInvocation.MyCommand.Path
if ([string]::IsNullOrEmpty($scriptPath)) { $scriptPath = $PSCommandPath }
$root = if ([string]::IsNullOrEmpty($scriptPath)) { (Get-Location).Path } else { Split-Path -Parent $scriptPath }
Set-Location -Path $root

$cliHome = Join-Path -Path $root -ChildPath '.dotnethome'
$tmpDir = Join-Path -Path $root -ChildPath '.tmp'
if ([string]::IsNullOrEmpty($cliHome) -or [string]::IsNullOrEmpty($tmpDir)) { throw "path resolve failed: root=$root" }
New-Item -ItemType Directory -Force -Path $cliHome, $tmpDir | Out-Null
$env:DOTNET_CLI_HOME = $cliHome
$env:TMP = $tmpDir
$env:TEMP = $tmpDir
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:MSBUILDDISABLENODEREUSE = '1'

$outDir = if ($Release) { 'bin\ReleasePkg' } else { 'bin\Dev' }

$buildArgs = @('build', '-c', 'Release', '-t:Rebuild', '--no-restore', '-o', $outDir, "-p:DSPGameDir=$GameDir")
if ($Release) { $buildArgs += '-p:ReleaseBuild=true' }

& $DotnetExe @buildArgs 2>&1 |
    Where-Object { $_ -match 'error|warning CS|DSP-RecipeSlots.dll|Build succeeded' }
if ($LASTEXITCODE -ne 0) { throw "build failed: $LASTEXITCODE" }

$dll = Join-Path (Join-Path $root $outDir) 'DSP-RecipeSlots.dll'
if (-not (Test-Path $dll)) { throw "dll missing: $dll" }
$mode = if ($Release) { 'release' } else { 'dev' }
Write-Host ("built ({0}): {1} ({2} bytes)" -f $mode, $dll, (Get-Item $dll).Length)

if ($Package) {
    if (-not $Release) { throw "-Package requires -Release (shipping dev build is not allowed)" }
    $manifest = Get-Content (Join-Path $root 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $ver = $manifest.version_number
    $name = $manifest.name
    $dist = Join-Path $root 'dist'
    $stage = Join-Path $dist 'pkg'
    if (Test-Path $stage) { Remove-Item $stage -Recurse -Force }
    $pluginDir = Join-Path $stage 'BepInEx\plugins\DSP-RecipeSlots'
    New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
    Copy-Item (Join-Path $root 'manifest.json') $stage
    Copy-Item (Join-Path $root 'icon.png') $stage
    Copy-Item (Join-Path $root 'README.md') $stage
    Copy-Item (Join-Path $root 'CHANGELOG.md') $stage
    Copy-Item $dll $pluginDir
    $zipName = "$name-$ver.zip"
    $zip = Join-Path $dist $zipName
    if (Test-Path $zip) { Remove-Item $zip -Force }
    Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $zip -Force
    $hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
    Set-Content (Join-Path $dist "$zipName.sha256") -Value ("$hash  $zipName") -Encoding ASCII
    Write-Host ("packaged: {0} ({1} bytes, sha256 {2})" -f $zip, (Get-Item $zip).Length, $hash)
}

if ($Deploy) {
    $dst = Join-Path $GameDir 'BepInEx\plugins\DSP-RecipeSlots'
    New-Item -ItemType Directory -Force -Path $dst | Out-Null
    $target = Join-Path $dst 'DSP-RecipeSlots.dll'
    try {
        Copy-Item $dll $target -Force
        if ($Release) {
            $devDir = Join-Path $dst '_dev'
            if (Test-Path $devDir) { Remove-Item $devDir -Recurse -Force }
        }
        Write-Host "deployed ($mode): $target"
    } catch {
        Write-Warning "deploy failed (game running? stop it first): $_"
        throw
    }
}



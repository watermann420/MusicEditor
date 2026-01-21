# MusicEngineEditor Build Script
# This script automatically restores NuGet packages and builds the project

param(
    [switch]$Release,
    [switch]$Clean,
    [switch]$Run,
    [switch]$Publish,
    [switch]$Installer
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MusicEngineEditor Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if dotnet is installed
Write-Host "[1/5] Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "      .NET SDK $dotnetVersion found" -ForegroundColor Green
} catch {
    Write-Host "ERROR: .NET SDK not found!" -ForegroundColor Red
    Write-Host "Please install .NET 8.0 SDK from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
    exit 1
}

# Check .NET 8 is available
$sdks = dotnet --list-sdks
if ($sdks -notmatch "8\.0") {
    Write-Host "WARNING: .NET 8.0 SDK not found. You may need to install it." -ForegroundColor Yellow
    Write-Host "Download from: https://dotnet.microsoft.com/download/dotnet/8.0" -ForegroundColor Yellow
}

$configuration = if ($Release) { "Release" } else { "Debug" }
Write-Host "      Configuration: $configuration" -ForegroundColor Cyan

# Clean if requested
if ($Clean) {
    Write-Host ""
    Write-Host "[2/5] Cleaning solution..." -ForegroundColor Yellow

    # Clean MusicEngine
    Push-Location "$scriptDir\..\MusicEngine"
    dotnet clean -c $configuration 2>$null
    Pop-Location

    # Clean MusicEngineEditor
    Push-Location "$scriptDir\MusicEngineEditor"
    dotnet clean -c $configuration 2>$null
    Pop-Location

    # Remove obj and bin folders
    Get-ChildItem -Path $scriptDir -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path "$scriptDir\..\MusicEngine" -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    Write-Host "      Clean completed" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[2/5] Skipping clean (use -Clean to clean)" -ForegroundColor Gray
}

# Restore NuGet packages
Write-Host ""
Write-Host "[3/5] Restoring NuGet packages..." -ForegroundColor Yellow

# Restore MusicEngine first
Write-Host "      Restoring MusicEngine..." -ForegroundColor Cyan
Push-Location "$scriptDir\..\MusicEngine"
$restoreResult = dotnet restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to restore MusicEngine packages" -ForegroundColor Red
    Write-Host $restoreResult -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host "      MusicEngine packages restored" -ForegroundColor Green

# Restore MusicEngineEditor
Write-Host "      Restoring MusicEngineEditor..." -ForegroundColor Cyan
Push-Location "$scriptDir\MusicEngineEditor"
$restoreResult = dotnet restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to restore MusicEngineEditor packages" -ForegroundColor Red
    Write-Host $restoreResult -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host "      MusicEngineEditor packages restored" -ForegroundColor Green

# Build MusicEngine
Write-Host ""
Write-Host "[4/5] Building MusicEngine..." -ForegroundColor Yellow
Push-Location "$scriptDir\..\MusicEngine"
$buildResult = dotnet build -c $configuration --no-restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: MusicEngine build failed" -ForegroundColor Red
    Write-Host $buildResult -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host "      MusicEngine built successfully" -ForegroundColor Green

# Build MusicEngineEditor
Write-Host ""
Write-Host "[5/5] Building MusicEngineEditor..." -ForegroundColor Yellow
Push-Location "$scriptDir\MusicEngineEditor"
$buildResult = dotnet build -c $configuration --no-restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: MusicEngineEditor build failed" -ForegroundColor Red
    Write-Host $buildResult -ForegroundColor Red
    Pop-Location
    exit 1
}
Pop-Location
Write-Host "      MusicEngineEditor built successfully" -ForegroundColor Green

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output: $scriptDir\MusicEngineEditor\bin\$configuration\net10.0-windows\" -ForegroundColor Cyan

# Run if requested
if ($Run) {
    Write-Host ""
    Write-Host "Starting MusicEngineEditor..." -ForegroundColor Yellow
    $exePath = "$scriptDir\MusicEngineEditor\bin\$configuration\net10.0-windows\MusicEngineEditor.exe"
    if (Test-Path $exePath) {
        Start-Process $exePath
    } else {
        Write-Host "ERROR: Executable not found at $exePath" -ForegroundColor Red
    }
}

# Publish if requested
if ($Publish) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Publishing Application" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $publishDir = "$scriptDir\MusicEngineEditor\bin\publish"

    # Clean previous publish output
    if (Test-Path $publishDir) {
        Write-Host "Cleaning previous publish output..." -ForegroundColor Gray
        Remove-Item -Path $publishDir -Recurse -Force
    }

    Write-Host "Publishing self-contained win-x64 application..." -ForegroundColor Yellow
    Push-Location "$scriptDir\MusicEngineEditor"
    $publishResult = dotnet publish -c Release -r win-x64 --self-contained true -p:PublishDir="$publishDir\" -p:PublishReadyToRun=true 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Publish failed" -ForegroundColor Red
        Write-Host $publishResult -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Pop-Location

    Write-Host "Application published successfully" -ForegroundColor Green
    Write-Host "Output: $publishDir" -ForegroundColor Cyan
}

# Build installer if requested
if ($Installer) {
    Write-Host ""
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host "  Building Installer" -ForegroundColor Cyan
    Write-Host "========================================" -ForegroundColor Cyan
    Write-Host ""

    $installerProject = "$scriptDir\installer\MusicEngineEditor.Installer.wixproj"
    $installerOutput = "$scriptDir\installer\bin\Release"

    # Check if publish output exists
    $publishDir = "$scriptDir\MusicEngineEditor\bin\publish"
    if (-not (Test-Path "$publishDir\MusicEngineEditor.exe")) {
        Write-Host "ERROR: Published application not found. Run with -Publish first." -ForegroundColor Red
        exit 1
    }

    # Clean previous installer output
    if (Test-Path $installerOutput) {
        Remove-Item -Path $installerOutput -Recurse -Force
    }

    Write-Host "Building WiX installer..." -ForegroundColor Yellow
    $installerResult = dotnet build $installerProject -c Release 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING: Installer build failed" -ForegroundColor Yellow
        Write-Host "This may be because WiX v4 SDK is not installed." -ForegroundColor Yellow
        Write-Host "Install with: dotnet tool install --global wix" -ForegroundColor Yellow
        Write-Host "Then run: wix extension add WixToolset.UI.wixext" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Build output:" -ForegroundColor Gray
        Write-Host $installerResult -ForegroundColor Gray
    } else {
        Write-Host "Installer built successfully" -ForegroundColor Green

        $msiFile = Get-ChildItem -Path $installerOutput -Filter "*.msi" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($msiFile) {
            Write-Host "Output: $($msiFile.FullName)" -ForegroundColor Cyan
        }
    }
}

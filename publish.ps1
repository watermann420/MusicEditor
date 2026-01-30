# MusicEngine Editor Publish Script
# This script builds, publishes, optionally signs, and creates the installer

param(
    [switch]$SkipBuild,
    [switch]$SkipSign,
    [switch]$SkipInstaller,
    [string]$CertificatePath = "",
    [string]$CertificatePassword = "",
    [string]$TimestampServer = "http://timestamp.digicert.com"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

# Configuration
$projectPath = "$scriptDir\MusicEngineEditor\MusicEngineEditor.csproj"
$publishProfile = "Release"
$publishDir = "$scriptDir\MusicEngineEditor\bin\publish"
$installerProject = "$scriptDir\installer\MusicEngineEditor.Installer.wixproj"
$installerOutput = "$scriptDir\installer\bin\Release"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MusicEngine Editor Publish Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Build and Publish
if (-not $SkipBuild) {
    Write-Host "[1/4] Building and publishing application..." -ForegroundColor Yellow

    # Clean previous publish output
    if (Test-Path $publishDir) {
        Write-Host "      Cleaning previous publish output..." -ForegroundColor Gray
        Remove-Item -Path $publishDir -Recurse -Force
    }

    # Restore packages
    Write-Host "      Restoring NuGet packages..." -ForegroundColor Gray
    $restoreResult = dotnet restore $projectPath 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Package restore failed" -ForegroundColor Red
        Write-Host $restoreResult -ForegroundColor Red
        exit 1
    }

    # Publish using the Release profile
    Write-Host "      Publishing self-contained win-x64 application..." -ForegroundColor Gray
    $publishResult = dotnet publish $projectPath `
        -c Release `
        -r win-x64 `
        --self-contained true `
        -p:PublishDir="$publishDir\" `
        -p:PublishReadyToRun=true `
        2>&1

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Publish failed" -ForegroundColor Red
        Write-Host $publishResult -ForegroundColor Red
        exit 1
    }

    Write-Host "      Application published successfully" -ForegroundColor Green
    Write-Host "      Output: $publishDir" -ForegroundColor Cyan
} else {
    Write-Host "[1/4] Skipping build (using existing publish output)" -ForegroundColor Gray
}

# Step 2: Code Signing
Write-Host ""
if (-not $SkipSign) {
    Write-Host "[2/4] Code signing..." -ForegroundColor Yellow

    # Check if certificate is provided
    if ([string]::IsNullOrEmpty($CertificatePath)) {
        Write-Host "      WARNING: No certificate provided. Skipping code signing." -ForegroundColor Yellow
        Write-Host "      To sign, use: -CertificatePath <path> -CertificatePassword <password>" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "      === Code Signing Placeholder ===" -ForegroundColor Magenta
        Write-Host "      In production, you would sign with:" -ForegroundColor Gray
        Write-Host "      signtool sign /f <certificate.pfx> /p <password> /fd SHA256 /tr $TimestampServer /td SHA256 <file.exe>" -ForegroundColor Gray
        Write-Host ""
    } else {
        # Verify certificate exists
        if (-not (Test-Path $CertificatePath)) {
            Write-Host "ERROR: Certificate not found at $CertificatePath" -ForegroundColor Red
            exit 1
        }

        # Find signtool
        $signtool = $null
        $windowsKits = @(
            "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.22621.0\x64\signtool.exe",
            "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.22000.0\x64\signtool.exe",
            "${env:ProgramFiles(x86)}\Windows Kits\10\bin\10.0.19041.0\x64\signtool.exe",
            "${env:ProgramFiles(x86)}\Windows Kits\10\bin\x64\signtool.exe"
        )

        foreach ($kit in $windowsKits) {
            if (Test-Path $kit) {
                $signtool = $kit
                break
            }
        }

        if ($null -eq $signtool) {
            Write-Host "ERROR: signtool.exe not found. Install Windows SDK." -ForegroundColor Red
            exit 1
        }

        Write-Host "      Using signtool: $signtool" -ForegroundColor Gray

        # Files to sign
        $filesToSign = @(
            "$publishDir\MusicEngineEditor.exe",
            "$publishDir\MusicEngineEditor.dll"
        )

        foreach ($file in $filesToSign) {
            if (Test-Path $file) {
                Write-Host "      Signing: $file" -ForegroundColor Gray

                $signArgs = @(
                    "sign",
                    "/f", $CertificatePath,
                    "/p", $CertificatePassword,
                    "/fd", "SHA256",
                    "/tr", $TimestampServer,
                    "/td", "SHA256",
                    $file
                )

                $signResult = & $signtool $signArgs 2>&1
                if ($LASTEXITCODE -ne 0) {
                    Write-Host "ERROR: Failed to sign $file" -ForegroundColor Red
                    Write-Host $signResult -ForegroundColor Red
                    exit 1
                }
            }
        }

        Write-Host "      Code signing completed" -ForegroundColor Green
    }
} else {
    Write-Host "[2/4] Skipping code signing" -ForegroundColor Gray
}

# Step 3: Build WiX Installer
Write-Host ""
if (-not $SkipInstaller) {
    Write-Host "[3/4] Building WiX installer..." -ForegroundColor Yellow

    # Check if WiX toolset is available
    $wixInstalled = $false
    try {
        $dotnetTools = dotnet tool list -g 2>&1
        if ($dotnetTools -match "wix") {
            $wixInstalled = $true
        }
    } catch {}

    if (-not $wixInstalled) {
        Write-Host "      WiX Toolset not found as global tool." -ForegroundColor Yellow
        Write-Host "      Attempting to build using dotnet build..." -ForegroundColor Yellow
    }

    # Clean previous installer output
    if (Test-Path $installerOutput) {
        Remove-Item -Path $installerOutput -Recurse -Force
    }

    # Build the installer
    Write-Host "      Building installer project..." -ForegroundColor Gray

    $installerResult = dotnet build $installerProject -c Release 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "WARNING: Installer build failed" -ForegroundColor Yellow
        Write-Host "      This may be because WiX v4 SDK is not installed." -ForegroundColor Yellow
        Write-Host "      Install with: dotnet tool install --global wix" -ForegroundColor Yellow
        Write-Host "      Then run: wix extension add WixToolset.UI.wixext" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "      Build output:" -ForegroundColor Gray
        Write-Host $installerResult -ForegroundColor Gray
    } else {
        Write-Host "      Installer built successfully" -ForegroundColor Green

        # Find the MSI file
        $msiFile = Get-ChildItem -Path $installerOutput -Filter "*.msi" -Recurse | Select-Object -First 1
        if ($msiFile) {
            Write-Host "      Output: $($msiFile.FullName)" -ForegroundColor Cyan

            # Sign the MSI if certificate is provided
            if (-not $SkipSign -and -not [string]::IsNullOrEmpty($CertificatePath) -and $null -ne $signtool) {
                Write-Host "      Signing MSI..." -ForegroundColor Gray

                $signArgs = @(
                    "sign",
                    "/f", $CertificatePath,
                    "/p", $CertificatePassword,
                    "/fd", "SHA256",
                    "/tr", $TimestampServer,
                    "/td", "SHA256",
                    $msiFile.FullName
                )

                $signResult = & $signtool $signArgs 2>&1
                if ($LASTEXITCODE -ne 0) {
                    Write-Host "WARNING: Failed to sign MSI" -ForegroundColor Yellow
                    Write-Host $signResult -ForegroundColor Gray
                } else {
                    Write-Host "      MSI signed successfully" -ForegroundColor Green
                }
            }
        }
    }
} else {
    Write-Host "[3/4] Skipping installer build" -ForegroundColor Gray
}

# Step 4: Summary
Write-Host ""
Write-Host "[4/4] Build Summary" -ForegroundColor Yellow
Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Publish completed!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Published files: $publishDir" -ForegroundColor Cyan

if (-not $SkipInstaller) {
    $msiFile = Get-ChildItem -Path $installerOutput -Filter "*.msi" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($msiFile) {
        Write-Host "Installer: $($msiFile.FullName)" -ForegroundColor Cyan
    }
}

Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Test the published application in: $publishDir" -ForegroundColor Gray
Write-Host "  2. Test the installer (if built)" -ForegroundColor Gray
Write-Host "  3. For production, sign with a valid code signing certificate" -ForegroundColor Gray
Write-Host ""

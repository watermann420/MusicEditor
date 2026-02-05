# MusicEngineEditor Build Script
# This script automatically restores NuGet packages and builds/tests the project

param(
    [switch]$Release,
    [switch]$Clean,
    [switch]$Run,
    [switch]$Publish,
    [switch]$Installer,
    [switch]$UiSmoke,
    [switch]$AudioSmoke,
    [switch]$PerfSmoke,
    [switch]$SkipNative,
    [switch]$NativeOnly
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "  MusicEngineEditor Build Script" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

$totalSteps = 8

# Check if dotnet is installed
Write-Host "[1/$totalSteps] Checking .NET SDK..." -ForegroundColor Yellow
try {
    $dotnetVersion = dotnet --version
    Write-Host "      .NET SDK $dotnetVersion found" -ForegroundColor Green
} catch {
    Write-Host "ERROR: .NET SDK not found!" -ForegroundColor Red
    Write-Host "Please install .NET 10.0 SDK (preview) from: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
    exit 1
}

# Check .NET 10 is available
$sdks = dotnet --list-sdks
if ($sdks -notmatch "10\.0") {
    Write-Host "WARNING: .NET 10.0 SDK not found. You may need to install it (preview)." -ForegroundColor Yellow
    Write-Host "Download from: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Yellow
}

$configuration = if ($Release) { "Release" } else { "Debug" }
Write-Host "      Configuration: $configuration" -ForegroundColor Cyan

# Ensure MusicEngine dependency is present
$musicEnginePath = Join-Path $scriptDir "..\MusicEngine"
Write-Host ""
Write-Host "[2/$totalSteps] Ensuring MusicEngine dependency..." -ForegroundColor Yellow
if (-not (Test-Path $musicEnginePath)) {
    Write-Host "      MusicEngine not found next to this repo; cloning from GitHub..." -ForegroundColor Cyan
    git clone https://github.com/watermann420/MusicEngine.git $musicEnginePath 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Failed to clone MusicEngine dependency" -ForegroundColor Red
        exit 1
    }
    Write-Host "      MusicEngine cloned to $musicEnginePath" -ForegroundColor Green
} else {
    Write-Host "      MusicEngine already present at $musicEnginePath" -ForegroundColor Green
}

# Clean if requested
if ($Clean) {
    Write-Host ""
    Write-Host "[3/$totalSteps] Cleaning solution..." -ForegroundColor Yellow

    # Clean MusicEngine (specify .csproj explicitly)
    $musicEngineCsproj = "$scriptDir\..\MusicEngine\MusicEngine.csproj"
    if (Test-Path $musicEngineCsproj) {
        dotnet clean $musicEngineCsproj -c $configuration 2>$null
    }

    # Clean MusicEngineEditor (specify .csproj explicitly)
    $editorCsproj = "$scriptDir\MusicEngineEditor\MusicEngineEditor.csproj"
    if (Test-Path $editorCsproj) {
        dotnet clean $editorCsproj -c $configuration 2>$null
    }

    # Remove obj and bin folders
    Get-ChildItem -Path $scriptDir -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
    Get-ChildItem -Path "$scriptDir\..\MusicEngine" -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue

    # Remove WPF temp .csproj files that cause MSB1011 errors
    Get-ChildItem -Path $scriptDir -Filter "*_wpftmp.csproj" -Recurse | Remove-Item -Force -ErrorAction SilentlyContinue

    Write-Host "      Clean completed" -ForegroundColor Green
} else {
    Write-Host ""
    Write-Host "[3/$totalSteps] Skipping clean (use -Clean to clean)" -ForegroundColor Gray
}

# Restore NuGet packages
Write-Host ""
Write-Host "[4/$totalSteps] Restoring NuGet packages..." -ForegroundColor Yellow

# Restore MusicEngine first (specify .csproj explicitly to avoid MSB1011)
Write-Host "      Restoring MusicEngine..." -ForegroundColor Cyan
$musicEngineCsproj = "$scriptDir\..\MusicEngine\MusicEngine.csproj"
$restoreResult = dotnet restore $musicEngineCsproj 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to restore MusicEngine packages" -ForegroundColor Red
    Write-Host $restoreResult -ForegroundColor Red
    exit 1
}
Write-Host "      MusicEngine packages restored" -ForegroundColor Green

# Restore MusicEngineEditor (specify .csproj explicitly to avoid MSB1011)
Write-Host "      Restoring MusicEngineEditor..." -ForegroundColor Cyan
$editorCsproj = "$scriptDir\MusicEngineEditor\MusicEngineEditor.csproj"
$restoreResult = dotnet restore $editorCsproj 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Failed to restore MusicEngineEditor packages" -ForegroundColor Red
    Write-Host $restoreResult -ForegroundColor Red
    exit 1
}
Write-Host "      MusicEngineEditor packages restored" -ForegroundColor Green

# Build CppLayer Native (C++) if not skipped
Write-Host ""
if (-not $SkipNative) {
    Write-Host "[5/$totalSteps] Building MusicEngine.CppLayer Native (C++)..." -ForegroundColor Yellow
    $cppLayerPath = "$scriptDir\..\MusicEngine\MusicEngine.CppLayer"
    $cppLayerBuildPath = "$cppLayerPath\build_cmake"

    # Check if CMake is available
    $cmakeAvailable = $false
    try {
        $cmakeVersion = cmake --version 2>$null | Select-Object -First 1
        if ($cmakeVersion) {
            $cmakeAvailable = $true
            Write-Host "      CMake found: $cmakeVersion" -ForegroundColor Gray
        }
    } catch {
        Write-Host "      CMake not found - skipping native build" -ForegroundColor Yellow
    }

    if ($cmakeAvailable -and (Test-Path "$cppLayerPath\CMakeLists.txt")) {
        # Create build directory
        if (-not (Test-Path $cppLayerBuildPath)) {
            New-Item -ItemType Directory -Path $cppLayerBuildPath -Force | Out-Null
        }

        # Configure with CMake
        Write-Host "      Configuring CMake..." -ForegroundColor Gray
        Push-Location $cppLayerBuildPath
        $cmakeConfigResult = cmake -S "$cppLayerPath" -B . -A x64 2>&1
        if ($LASTEXITCODE -ne 0) {
            Write-Host "WARNING: CMake configuration failed" -ForegroundColor Yellow
            Write-Host $cmakeConfigResult -ForegroundColor Gray
            Write-Host "      Native CppLayer will not be built" -ForegroundColor Yellow
        } else {
            # Build with CMake
            Write-Host "      Building native library..." -ForegroundColor Gray
            $cmakeBuildResult = cmake --build . --config $configuration 2>&1
            if ($LASTEXITCODE -ne 0) {
                Write-Host "WARNING: Native build failed" -ForegroundColor Yellow
                Write-Host $cmakeBuildResult -ForegroundColor Gray
                Write-Host "      Continuing without native CppLayer" -ForegroundColor Yellow
            } else {
                Write-Host "      CppLayer Native built successfully" -ForegroundColor Green

                # Copy DLL to managed project runtimes folder
                $nativeDll = "$cppLayerPath\native\x64\MusicEngine.CppLayer.Native.dll"
                $runtimeDir = "$cppLayerPath\managed\runtimes\win-x64\native"
                if (Test-Path $nativeDll) {
                    if (-not (Test-Path $runtimeDir)) {
                        New-Item -ItemType Directory -Path $runtimeDir -Force | Out-Null
                    }
                    Copy-Item $nativeDll $runtimeDir -Force
                    Write-Host "      Native DLL copied to managed project" -ForegroundColor Green
                }
            }
        }
        Pop-Location
    } else {
        if (-not (Test-Path "$cppLayerPath\CMakeLists.txt")) {
            Write-Host "      CppLayer CMakeLists.txt not found - skipping native build" -ForegroundColor Gray
        }
    }

    if ($NativeOnly) {
        Write-Host ""
        Write-Host "Native build completed (NativeOnly mode)" -ForegroundColor Green
        exit 0
    }
} else {
    Write-Host "[5/$totalSteps] Skipping native build (-SkipNative)" -ForegroundColor Gray
}

# Build MusicEngine (specify .csproj explicitly to avoid MSB1011)
Write-Host ""
Write-Host "[6/$totalSteps] Building MusicEngine..." -ForegroundColor Yellow
$musicEngineCsproj = "$scriptDir\..\MusicEngine\MusicEngine.csproj"
$buildResult = dotnet build $musicEngineCsproj -c $configuration --no-restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: MusicEngine build failed" -ForegroundColor Red
    Write-Host $buildResult -ForegroundColor Red
    exit 1
}
Write-Host "      MusicEngine built successfully" -ForegroundColor Green

# Build MusicEngineEditor (specify .csproj explicitly to avoid MSB1011)
Write-Host ""
Write-Host "[7/$totalSteps] Building MusicEngineEditor..." -ForegroundColor Yellow
$editorCsproj = "$scriptDir\MusicEngineEditor\MusicEngineEditor.csproj"
$buildResult = dotnet build $editorCsproj -c $configuration --no-restore 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: MusicEngineEditor build failed" -ForegroundColor Red
    Write-Host $buildResult -ForegroundColor Red
    exit 1
}
Write-Host "      MusicEngineEditor built successfully" -ForegroundColor Green

# Run tests
Write-Host ""
Write-Host "[8/$totalSteps] Running tests..." -ForegroundColor Yellow
Push-Location "$scriptDir\MusicEngineEditor.Tests"
$testResult = dotnet test -c $configuration --logger "trx;LogFileName=TestResults.trx" 2>&1
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Tests failed" -ForegroundColor Red
    Write-Host $testResult -ForegroundColor Red
    Pop-Location
    exit 1
}
Write-Host "      Unit tests completed successfully" -ForegroundColor Green

# Optional UI smoke tests (require interactive session)
if ($UiSmoke -or $env:ENABLE_UI_TESTS -eq "1" -or $env:ENABLE_UI_TESTS -eq "true") {
    Write-Host ""
    Write-Host "      Running UI smoke tests (Category=UI)..." -ForegroundColor Yellow
    $uiResult = dotnet test -c $configuration --logger "trx;LogFileName=UITests.trx" --filter "Category=UI" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: UI smoke tests failed" -ForegroundColor Red
        Write-Host $uiResult -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Write-Host "      UI smoke tests completed successfully" -ForegroundColor Green
}

# Optional audio analysis smoke (no audio device required; analyzes synthesized sine)
if ($AudioSmoke -or $env:ENABLE_AUDIO_TESTS -eq "1" -or $env:ENABLE_AUDIO_TESTS -eq "true") {
    Write-Host ""
    Write-Host "      Running Audio smoke tests (Category=Audio)..." -ForegroundColor Yellow
    $audioResult = dotnet test -c $configuration --logger "trx;LogFileName=AudioTests.trx" --filter "Category=Audio" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Audio smoke tests failed" -ForegroundColor Red
        Write-Host $audioResult -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Write-Host "      Audio smoke tests completed successfully" -ForegroundColor Green
}

# Optional performance/timing smoke tests
if ($PerfSmoke -or $env:ENABLE_PERF_TESTS -eq "1" -or $env:ENABLE_PERF_TESTS -eq "true") {
    Write-Host ""
    Write-Host "      Running Performance smoke tests (Category=Perf)..." -ForegroundColor Yellow
    $perfResult = dotnet test -c $configuration --logger "trx;LogFileName=PerfTests.trx" --filter "Category=Perf" 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Performance smoke tests failed" -ForegroundColor Red
        Write-Host $perfResult -ForegroundColor Red
        Pop-Location
        exit 1
    }
    Write-Host "      Performance smoke tests completed successfully" -ForegroundColor Green
}
Pop-Location

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "  Build completed successfully!" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
Write-Host ""
Write-Host "Output: $scriptDir\MusicEngineEditor\bin\$configuration\net10.0-windows\" -ForegroundColor Cyan
Write-Host "Tests:  $scriptDir\MusicEngineEditor.Tests\TestResults.trx" -ForegroundColor Cyan

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
    $editorCsproj = "$scriptDir\MusicEngineEditor\MusicEngineEditor.csproj"
    $publishResult = dotnet publish $editorCsproj -c Release -r win-x64 --self-contained true -p:PublishDir="$publishDir\" -p:PublishReadyToRun=true 2>&1
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Publish failed" -ForegroundColor Red
        Write-Host $publishResult -ForegroundColor Red
        exit 1
    }

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

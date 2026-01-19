@echo off
:: ========================================
::   MusicEngine Editor - Quick Start
:: ========================================
:: Fuer Anfaenger: Einfach doppelklicken!
:: Baut automatisch und startet den Editor.

cd /d "%~dp0"
title MusicEngine Editor

:: Check if already built (Debug or Release)
if exist "MusicEngineEditor\bin\Release\net10.0-windows\MusicEngineEditor.exe" (
    echo Starting MusicEngine Editor...
    start "" "MusicEngineEditor\bin\Release\net10.0-windows\MusicEngineEditor.exe"
    exit /b 0
)

if exist "MusicEngineEditor\bin\Debug\net10.0-windows\MusicEngineEditor.exe" (
    echo Starting MusicEngine Editor...
    start "" "MusicEngineEditor\bin\Debug\net10.0-windows\MusicEngineEditor.exe"
    exit /b 0
)

:: Not built yet - build first
echo ========================================
echo   MusicEngine Editor - First Start
echo ========================================
echo.
echo Editor not built yet. Building now...
echo This only happens once.
echo.

:: Check if dotnet is available
where dotnet >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo ========================================
    echo   .NET 10 SDK Required
    echo ========================================
    echo.
    echo Please install .NET 10 SDK from:
    echo https://dotnet.microsoft.com/download
    echo.
    echo After installing, run this script again.
    echo.
    pause
    exit /b 1
)

:: Run the build script
call build.bat Release

:: Start if build succeeded
if exist "MusicEngineEditor\bin\Release\net10.0-windows\MusicEngineEditor.exe" (
    echo.
    echo ========================================
    echo   Starting MusicEngine Editor...
    echo ========================================
    start "" "MusicEngineEditor\bin\Release\net10.0-windows\MusicEngineEditor.exe"
) else (
    echo.
    echo Build failed. Please check the error messages above.
    pause
)

@echo off
:: ========================================
::   MusicEngine Editor - Quick Start
:: ========================================
:: Fuer Anfaenger: Einfach doppelklicken!
:: Startet den Editor ohne Build, wenn eine EXE vorhanden ist.

cd /d "%~dp0"
title MusicEngine Editor

:: Check for a prebuilt editor (Release or Debug)
if exist "x64\Release\MusicEngineEditor.exe" (
    echo Starting MusicEngine Editor...
    start "" "x64\Release\MusicEngineEditor.exe"
    exit /b 0
)

if exist "x64\Debug\MusicEngineEditor.exe" (
    echo Starting MusicEngine Editor...
    start "" "x64\Debug\MusicEngineEditor.exe"
    exit /b 0
)

echo ========================================
echo   MusicEngine Editor - Build Required
echo ========================================
echo.
echo No prebuilt EXE found.
echo To build the editor you need Visual Studio Build Tools
echo with "Desktop development with C++" installed.
echo.
echo If you only want to run, copy a built EXE into:
echo   x64\Release\MusicEngineEditor.exe
echo   or
echo   x64\Debug\MusicEngineEditor.exe
echo.
pause

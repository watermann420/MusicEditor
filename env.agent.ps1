$ErrorActionPreference = "Stop"

# Adds common fast CLI tools installed via winget to PATH for this session.
# Usage: .\\env.agent.ps1  (from repo root) or `. .\\env.agent.ps1` to keep PATH in current session.

$rgDir = Join-Path $env:LOCALAPPDATA "Microsoft\\WinGet\\Packages\\BurntSushi.ripgrep.MSVC_Microsoft.Winget.Source_8wekyb3d8bbwe\\ripgrep-15.1.0-x86_64-pc-windows-msvc"
$fdDir = Join-Path $env:LOCALAPPDATA "Microsoft\\WinGet\\Packages\\sharkdp.fd_Microsoft.Winget.Source_8wekyb3d8bbwe\\fd-v10.3.0-x86_64-pc-windows-msvc"

$pathsToAdd = @($rgDir, $fdDir) | Where-Object { Test-Path $_ }
$newSegments = $pathsToAdd | Where-Object { -not ($env:PATH -split ";" | Where-Object { $_ -eq $_ }) }

if ($newSegments.Count -gt 0) {
    $env:PATH = ($newSegments -join ";") + ";" + $env:PATH
    Write-Host "Added to PATH for this session:" -ForegroundColor Cyan
    $newSegments | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Host "rg/fd already available in PATH for this session." -ForegroundColor Gray
}

Write-Host "`nVerify:" -ForegroundColor Yellow
Write-Host "  rg --version" -ForegroundColor Yellow
Write-Host "  fd --version" -ForegroundColor Yellow

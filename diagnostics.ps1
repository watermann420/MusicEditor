# Quick diagnostics: print warnings and errors only for a solution/project.
param(
    [string] $Solution = "MusicEngineEditor.sln",
    [switch] $Restore
)

$msbuildArgs = @(
    $Solution
    "-v:minimal"
    "/nologo"
    "/clp:WarningsOnly;Summary"
)

if ($Restore) {
    $msbuildArgs = @("restore", $Solution, "/nologo") , @("build") + $msbuildArgs
}

Write-Host "Running: dotnet build $($msbuildArgs -join ' ')"
dotnet build @msbuildArgs

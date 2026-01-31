# AI Knowledge Base (MusicEngine / MusicEngineEditor)
Structured cheat-sheet for agents to recall project facts fast. Update this file whenever you add features, tests, or workflows.

## 1) Paths
- Solution: `MusicEngineEditor.sln`
- Editor project: `MusicEngineEditor/MusicEngineEditor.csproj`
- Tests: `MusicEngineEditor.Tests/MusicEngineEditor.Tests.csproj`
- Engine (sibling repo expected): `../MusicEngine/MusicEngine.csproj`
- Binaries: `MusicEngineEditor/bin/{Config}/net10.0-windows/`
- Test artifacts: `MusicEngineEditor.Tests/*.trx`

## 2) Build / Run / Test Commands
- Build + unit tests: `pwsh ./build.ps1 -Release` (or `-Clean -Release`)
- UI smoke (visible window): `pwsh ./build.ps1 -Release -UiSmoke` or `SET ENABLE_UI_TESTS=1`
- Audio/Pattern smoke: `pwsh ./build.ps1 -Release -AudioSmoke` or `SET ENABLE_AUDIO_TESTS=1`
- Performance smoke: `pwsh ./build.ps1 -Release -PerfSmoke` or `SET ENABLE_PERF_TESTS=1`
- Combine: `pwsh ./build.ps1 -Release -UiSmoke -AudioSmoke`
- Run editor: `dotnet run --project MusicEngineEditor/MusicEngineEditor.csproj`

## 3) Test Coverage Map
- Unit: services, viewmodels, scripts (default run)
- UI smoke (`Category=UI`): launches `MusicEngineEditor.exe`, asserts main window
- Audio smoke (`Category=Audio`): FFT/RMS/peak on sine + A4→C5→E5→A5 pattern
- Perf smoke (`Category=Perf`): timing & memory (multi project creation + 5s FFT) with generous thresholds
- Artifacts: `TestResults.trx`, `UITests.trx`, `AudioTests.trx`

## 4) Project Creation Defaults (ProjectService)
- Creates `path/{Name}/{Name}/` with `Scripts`, `Audio`, `bin`, `obj`
- Default script: uses `test_script.csx` from sibling MusicEngine if present; else templated entrypoint
- Settings defaults: SampleRate 44100, BufferSize 512, BPM 120

## 5) Tooling
- Fast search: `rg`, `fd` (ensure PATH or source `.\env.agent.ps1`)
- Build script auto-clones `../MusicEngine` if missing.

## 6) Switches / Env Flags
- `-UiSmoke` / `ENABLE_UI_TESTS=1` → UI visible test
- `-AudioSmoke` / `ENABLE_AUDIO_TESTS=1` → audio analysis + pattern test
- `-Publish`, `-Installer`, `-Run` exist in build.ps1

## 7) Update Protocol
- When adding features/tests/commands, append concise bullets here.
- Keep sections short; prefer command + path over prose.

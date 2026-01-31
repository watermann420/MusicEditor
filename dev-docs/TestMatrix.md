# Test Matrix & Quick Commands

| Area          | What it covers                                  | Command (PowerShell)                                                                 | Artifacts                          | Notes                                           |
|---------------|-------------------------------------------------|--------------------------------------------------------------------------------------|------------------------------------|-------------------------------------------------|
| Unit          | All core services/viewmodels                    | `pwsh ./build.ps1 -Release`                                                          | `MusicEngineEditor.Tests/TestResults.trx` | Default run; always enabled                     |
| UI Smoke      | Launches editor, checks main window appears     | `pwsh ./build.ps1 -Release -UiSmoke`<br/>or `SET ENABLE_UI_TESTS=1; pwsh ./build.ps1 -Release` | `MusicEngineEditor.Tests/UITests.trx` | Requires interactive desktop (not headless)     |
| Audio Smoke   | FFT/RMS/peak on synthesized sine                | `pwsh ./build.ps1 -Release -AudioSmoke`<br/>or `SET ENABLE_AUDIO_TESTS=1; pwsh ./build.ps1 -Release` | `MusicEngineEditor.Tests/AudioTests.trx` | No audio device needed                          |
| Pattern Audio | Note pattern (A4→C5→E5→A5) freq/RMS validation  | Included in Audio category above (PatternSmokeTests)                                 | same as AudioTests.trx             | Validates per-step dominant frequency & levels   |
| Perf Smoke    | Timing/memory checks (project creation + FFT)   | `pwsh ./build.ps1 -Release -PerfSmoke`<br/>or `SET ENABLE_PERF_TESTS=1; pwsh ./build.ps1 -Release` | `MusicEngineEditor.Tests/PerfTests.trx`  | Generous thresholds; catches major regressions   |

Tips:
- Combine switches, e.g., `pwsh ./build.ps1 -Release -UiSmoke -AudioSmoke`.
- UI tests are skipped automatically if not enabled; same for Audio.
- Artifacts (.trx) live under `MusicEngineEditor.Tests/`. Use `dotnet test --logger trx` to regenerate ad-hoc.***

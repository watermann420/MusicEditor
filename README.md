
![BannerEditor](https://github.com/user-attachments/assets/d0751482-093f-4d9f-b980-4da5137bf8bf)

# MusicEngineEditor

![License](https://img.shields.io/badge/license-MEL-blue)
![C#](https://img.shields.io/badge/language-C%23-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![Status](https://img.shields.io/badge/status-Work_in_Progress-orange)

**MusicEngineEditor** is a professional code editor for the **MusicEngine** audio scripting system. Create music through code with real-time visualization, inline parameter controls, and VCV Rack-style modulation.

> **Note:** The core MusicEngine was written manually. The Editor and many features are AI-enhanced and may still have rough edges. Contributions welcome!

Discord: discord.gg/tWkqHMsB6a
---

## Features

### Code Editor
- Syntax highlighting optimized for MusicEngine scripts
- Intelligent autocomplete for classes, methods, and parameters (improved synth.* members)
- Strudel-style inline sliders (drag numbers to change values)
- **Inline visuals** (punchcard, piano roll glow, mixer meters) at 60 FPS, inserted via code comments
- Live code visualization: active `Note(...)` pitches, pattern steps, and `midi.device(n)` literals glow only while matching MIDI/sequence events are happening (comments ignored; only visible lines render)
- Dark/Light themes

### Audio Engine
- Real-time audio playback and preview
- Multiple synthesizer types (Simple, Advanced, Modular)
- Built-in effects (Reverb, Delay, Filter, etc.)
- MIDI input/output support with per-device logging (`midi.device(0).log.info()`) and LED control (`midi.device(0).led.set(note, val, channel)`)
- VCV Rack-style modular parameter system

### Workflow
- Project management
- Pattern and arrangement editor
- Waveform visualization
- Performance monitoring
- VST plugin support


---

## Quick Start

**No programming knowledge required:**

```bash
git clone https://github.com/watermann420/MusicEngineEditor.git
```

Then **double-click `StartEditor.bat`** - done!

---

## Inline Visuals (editor-only)

Add a command as a comment and the visual appears right under that line:

```csharp
// .punchcard
// .pianoroll
// .mixervisual height=160
```

- 60 FPS updates
- Notes glow while playing; pattern steps pulse; `midi.device(0)` index glows on any incoming MIDI (notes/CC/clock) when logging or routing is active
- Mixer meters show per-channel levels
- Only visible, non-comment lines render for performance

See `docs/InlineVisuals.md` for details.

---

## MIDI logging, transport bindings & LEDs (engine + editor scripting)

```csharp
// Toggle verbose MIDI logging for device 0 (notes/CC/pitch)
midi.device(0).log.info();          // on
midi.device(0).log.info(false);     // off

// Log only CC or clock if needed
midi.device(0).log.cc();            // CC on
midi.device(0).log.timingClock();   // clock on

// Map CC to transport
midi.device(0).cc(20).toStart();    // CC20 > 0.5 starts playback
midi.device(0).cc(21).toStop();     // CC21 > 0.5 stops playback
midi.device(0).cc(22).toRefresh();  // CC22 > 0.5 reloads script

// LEDs (send to matching MIDI output index; pick a safe channel to avoid sound)
var led = midi.device(0).led;
led.set(36, 100, 9);   // note/pad LED on (brightness/color depends on device)
led.off(36, 9);        // off
led.cc(1, 80, 9);      // some controllers use CC for lights
```

> Tip: choose an output index that corresponds to your controller’s MIDI Out, and a channel that isn’t routed to a synth (e.g., 9/10) to avoid audible notes. When `log.info(true)` is on, the device index literal (e.g., the `0` in `midi.device(0)`) pulses for any incoming message.

---

## Code Example

```csharp
// Create instruments
var bass = new SimpleSynth();
var lead = new SimpleSynth();

// Pattern can drive multiple synths
var pat = CreatePattern(bass, lead);

// Add notes (pitch, beat, duration, velocity)
pat.Note(60, 0,   0.5, 100);
pat.Note(64, 0.5, 0.5, 100);
pat.Note(67, 1,   0.5, 100);

// Step-sequencer shorthand (defaults: pitch 60, vel 100, len 0.25 beats)
pat.Seq("10100101", opt => {
    opt.pitch(72).velocity(90).step(0.25).duration(0.25);
});

// Random helper
var r = random.range(0, 2.5).speed(2); // max 2 updates/sec
double mod = r.next();                 // reuse across calls
bool hit = random.nextBool(0.3);       // 30% chance

// Play!
pat.Play();
```

---


## VCV Rack-style Modulation

Every parameter can be modulated by any source:

```csharp
// Create modulation sources
var lfo = new ModularLFO("lfo1", "Filter LFO", sampleRate);
lfo.Rate.Value = 2.0;      // 2 Hz
lfo.Depth.Value = 0.5;     // 50% depth

// Connect to filter cutoff
synth.Connect(lfo, synth.GetParameter("cutoff"), 0.5);

// Create envelope modulation
var env = new ModularEnvelope("env1", "Amp Env", sampleRate);
synth.Connect(env, synth.GetParameter("volume"), 1.0);
```

---

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [MusicEngine](https://github.com/watermann420/MusicEngine)

---

## Installation

### Option 1: StartEditor.bat (Recommended)

1. Clone the repository
2. Double-click `StartEditor.bat`

### Option 2: Manual

```bash
git clone https://github.com/watermann420/MusicEngineEditor.git
cd MusicEngineEditor
dotnet build
dotnet run --project MusicEngineEditor
```

## Build & Test (non-interactive)

- Prereqs: .NET 10.0 SDK (preview) and Git available on PATH.
- Single command to clean, restore, build, and run tests:

```bash
pwsh ./build.ps1 -Clean -Release
```

The script auto-clones the `MusicEngine` dependency if it's not already present next to this repository and writes test results to `MusicEngineEditor.Tests/TestResults.trx`.

### UI smoke tests (optional, visual)

To launch the editor during tests and sanity-check the UI:

```bash
# option 1: one-off
pwsh ./build.ps1 -Release -UiSmoke

# option 2: via env var
$env:ENABLE_UI_TESTS=1; pwsh ./build.ps1 -Release
```

The UI smoke test starts the built `MusicEngineEditor.exe`, waits for the main window, and then closes it. It produces `MusicEngineEditor.Tests/UITests.trx`. Requires an interactive desktop (not headless).

### Audio smoke tests (optional, analysis)

To verify audio analysis (FFT/RMS/peak) quickly:

```bash
pwsh ./build.ps1 -Release -AudioSmoke
# or
$env:ENABLE_AUDIO_TESTS=1; pwsh ./build.ps1 -Release
```

This synthesizes a sine tone (and a short note pattern) in tests and asserts dominant frequency, RMS, and peak using FFT (no sound device needed). Results in `MusicEngineEditor.Tests/AudioTests.trx`.

### Performance smoke tests (optional)

To check timing/memory regressions (project creation + 5s FFT):

```bash
pwsh ./build.ps1 -Release -PerfSmoke
# or
$env:ENABLE_PERF_TESTS=1; pwsh ./build.ps1 -Release
```

Produces `MusicEngineEditor.Tests/PerfTests.trx` with generous thresholds to flag major slowdowns.

---

## Project Structure

```
MusicEngineEditor/
├── Controls/              # UI controls
│   ├── Synths/            # Synthesizer editors (FM, Granular, Wavetable, etc.)
│   ├── Effects/           # Effect editors (Convolution, Multiband, Vocoder, etc.)
│   ├── Analysis/          # Analysis tools (Spectrogram3D, MixRadar, Phase, etc.)
│   ├── MIDI/              # MIDI controls (MPE, Expression Maps, Probability)
│   └── Performance/       # Performance tools (Looper, DJ, GrooveBox)
├── Editor/                # Code editor components
├── Models/                # Data models
├── Services/              # Business logic
├── ViewModels/            # MVVM ViewModels
│   └── Synths/            # Synthesizer ViewModels
├── Views/                 # XAML Views
│   └── Dialogs/           # Modal dialogs
└── Themes/                # Dark/Light themes

MusicEngine/               # Core audio engine (separate repo)
└── Core/
    ├── Synthesizers/      # 45+ synthesizers
    ├── Effects/           # 100+ effects
    ├── Analysis/          # Spectrum, Tempo, Chord detection
    ├── Sequencing/        # Step, Probability, Euclidean
    ├── Midi/              # MPE, MIDI 2.0, Expression Maps
    └── Modulation/        # VCV Rack-style system
```

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for:
- Project structure overview
- Syntax guide with examples
- Code style guidelines
- Pull request process

---

## Documentation

- [Modulation System](docs/MODULATION_SYSTEM.md) - VCV Rack-style parameter modulation
- [CONTRIBUTING Guide](CONTRIBUTING.md) - How to contribute

---

## License

[MusicEngine License (MEL)](LICENSE) - Honor-Based Commercial Support

---

## Links

- [MusicEngine Core](https://github.com/watermann420/MusicEngine)
- [Report Issues](https://github.com/watermann420/MusicEngineEditor/issues)

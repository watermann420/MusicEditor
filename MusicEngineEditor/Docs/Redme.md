
![BannerEditor](https://github.com/user-attachments/assets/d0751482-093f-4d9f-b980-4da5137bf8bf)

# MusicEngineEditor

![License](https://img.shields.io/badge/license-MEL-blue)
![C#](https://img.shields.io/badge/language-C%23-blue)
![.NET](https://img.shields.io/badge/.NET-10.0-purple)
![Status](https://img.shields.io/badge/status-Work_in_Progress-orange)


**MusicEngineEditor** is a professional code editor for the **MusicEngine** audio scripting system. Create music through code with real-time visualization, inline parameter controls, and VCV Rack-style modulation.

> **Note:** The core MusicEngine was written manually. The Editor and many features are AI-enhanced and may still have rough edges. Contributions welcome!

[![Join Discord](https://img.shields.io/badge/Discord-Join%20Server-5865F2?logo=discord&logoColor=white)](https://discord.gg/tWkqHMsB6a)
---

## Features

### Code Editor
- Syntax highlighting optimized for MusicEngine scripts
- Intelligent autocomplete for classes, methods, and parameters
- Live code visualization with note and pattern highlighting
- Dark/Light themes

### Audio Engine
- Real-time audio playback and preview
- 12+ synthesizer types with visual editors
- Built-in effects (Reverb, Delay, Filter, etc.)
- MIDI input/output with per-device logging and LED control
- VCV Rack-style modular parameter system

### Session View & Clip Launcher
- 8x8 grid for Ableton-style clip launching
- Scene triggers for row-based playback
- Visual clip states (empty, loaded, playing, queued)
- Drag-and-drop clip arrangement

### Workspace Presets
- **Ctrl+1-5** quick layout switching (Recording, Mixing, Mastering, Editing, Performance)
- Save and restore custom window layouts
- Panel visibility and sizing presets
- Workspace Manager (Ctrl+Shift+W)


### Spatial Audio
- **3D Positioning**: Place sounds anywhere in 3D space
- **Surround Panner**: 5.1/7.1 positioning with visual speaker layout
- **Binaural Renderer**: HRTF-based 3D audio with head tracking support
- **Ambisonics**: 1st-3rd order encoding/decoding with rotation controls
- Room simulation with size, damping, and near-field compensation

### Synthesizer Editors
- **Additive**: 64 partials with Hammond-style drawbars
- **FM**: 6-operator FM with 20 algorithms
- **Granular**: Grain-based synthesis with 5 envelope shapes
- **Wavetable**: Wavetable morphing with position control
- **Vector**: XY crossfade between 4 sources
- **ChipTune**: NES, GameBoy, C64 chip emulation
- **Organ**: Hammond-style drawbars with Leslie simulation
- **SID**: Commodore 64 SID chip with ring mod and sync
- **OPN**: YM2612/Genesis FM with 4 operators
- **EPiano**: Rhodes and Wurlitzer models
- **Sampler/Slicer**: Waveform slicing with pad triggering

### Network/Sync
- **Ableton Link**: Tempo sync with peer discovery and visual metronome
- **OSC Control**: Message mapping with learn mode and presets
- **Network MIDI**: RTP-MIDI with Bonjour/mDNS discovery
- **Machine Control**: MMC/MTC support with timecode display

### Professional DAW Features
- **Master Channel Strip**: LUFS/VU meters, master fader, effects
- **Return/Bus Tracks**: 4 auxiliary returns (A-D) with send routing
- **Unified Preset Browser**: Tree view, search, favorites, tags
- **Visual Undo History**: Timeline visualization with jump-to-state
- **Track Color Picker**: 16-color palette with customization

### Creative Effects
- **AutoTune**: Pitch correction with key/scale selection
- **BeatRepeat**: 8x16 gate pattern editor with stutter mode
- **Harmonizer**: 4-voice harmony with scale lock
- **GlitchMachine**: 8 effect modules with pattern sequencer
- **SpectralFreeze**: FFT freeze with 4 slots and morphing
- **Lo-Fi**: TapeStop, VinylEmulation, TapeSaturation, Bitcrusher

---

## Quick Start

**No programming knowledge required:**

```bash
git clone https://github.com/watermann420/MusicEngineEditor.git
```

Then **double-click `StartEditor.bat`** - done!

---



## Audio-Reactive UI

The editor features professional audio-reactive lighting effects inspired by modern DAWs like FL Studio, Ableton, and Bitwig.

### Audio Reactive Lighting
When a script is running, UI elements respond to the audio in real-time:

| Element | Frequency Band | Effect |
|---------|---------------|--------|
| Run Button | Bass (20-200Hz) + Beat | Glow pulses (BlurRadius 8-24) |
| Sidebar Icons | Mids (200-2kHz) | Wave-like brightness effect |
| Status Indicator | Overall RMS | Color brightness varies |

### Audio Visualizer Background
A subtle ambient background layer reacts to music (default 12% max opacity):

- **Bass Glow**: Purple/blue gradient from bottom edge
- **Mid Glow**: Cyan gradients on left/right edges
- **High Glow**: White/cyan sparkle on top edge
- **Ambient Pulse**: Center radial pulse that scales with beat (600-1000px)

Configuration in code:
```csharp
// Toggle visualizer
SetAudioVisualizerEnabled(true);

// Set intensity (0.0 - 0.3)
SetAudioVisualizerIntensity(0.12f);
```

### Technical Details
- **AudioReactiveService**: Singleton service processing audio data
- **AnalysisService**: FFT spectrum analysis for frequency bands
- **60 FPS updates** via DispatcherTimer with smooth interpolation
- **Beat detection** for pulsing effects on bass transients
- Zero allocations in render loop for performance

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
// Set BPM and start the transport
Sequencer.Bpm = 120;
Sequencer.Start();

// Create instruments
var bass = CreateSynth();
bass.SetParameter("waveform", 2); // 0=Sine, 1=Square, 2=Saw, 3=Triangle, 4=Noise
bass.SetParameter("cutoff", 0.5f);

var lead = CreateSynth();
lead.SetParameter("waveform", 1);
lead.SetParameter("cutoff", 0.7f);

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
pat.Start();
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
│   ├── Synths/            # Synthesizer editors (FM, Granular, ChipTune, Organ, SID, OPN, etc.)
│   ├── Effects/           # Effect editors (AutoTune, Harmonizer, GlitchMachine, Bitcrusher, etc.)
│   ├── Analysis/          # Analysis tools (GuitarTuner, ChordDetector, KeyDetector, TempoDetector, LoopFinder)
│   ├── Spatial/           # Spatial audio (SurroundPanner, BinauralRenderer, Ambisonic)
│   ├── Network/           # Network sync (LinkSync, OSCControl, NetworkMIDI, MachineControl)
│   ├── MIDI/              # MIDI controls (MPE, Expression Maps, Probability)
│   ├── Mixer/             # Mixer controls (MasterChannelStrip, ReturnTrack)
│   ├── Session/           # Session view (ClipSlot, ClipLauncher)
│   └── Performance/       # Performance tools (Looper, DJ, GrooveBox)
├── Editor/                # Code editor components
├── Models/                # Data models
├── Services/              # Business logic (SpatialAudioService, IntegratedAnalysisService, NetworkSyncService)
├── ViewModels/            # MVVM ViewModels
│   ├── Synths/            # Synthesizer ViewModels
│   ├── Effects/           # Effect ViewModels
│   ├── Analysis/          # Analysis ViewModels
│   └── Network/           # Network ViewModels
├── Views/                 # XAML Views
│   └── Dialogs/           # Modal dialogs
└── Themes/                # Dark/Light themes

MusicEngine/               # Core audio engine (separate repo)
└── Core/
    ├── Synthesizers/      # 45+ synthesizers
    ├── Effects/           # 100+ effects
    ├── Analysis/          # Spectrum, Tempo, Chord detection
    ├── Sequencing/        # Step, Probability, Euclidean
    ├── Spatial/           # Surround, Binaural, Ambisonics
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




### Command Palette & Transport
| Shortcut | Action |
|----------|--------|
| Ctrl+P | Command Palette (fuzzy search for all commands) |
| Ctrl+Enter | Run Script |
| Alt+Space | Panic (All Notes Off) |
| Escape | Stop |

---

## Analysis Tools

- **Audio Statistics (F10)**: Real-time audio analysis including RMS, peak levels, and frequency spectrum
- **Loudness Report (Ctrl+Shift+L)**: Generate LUFS-compliant loudness reports for mastering
- **Audio Suite**: Comprehensive audio analysis toolkit
- **Project Statistics**: Overview of project resources, track count, and memory usage

---

## Plugin Management

- **Plugin Manager (F12)**: Browse, install, and manage VST plugins via command palette
- Automatic plugin scanning and validation
- Plugin preset management
- VST2/VST3 support

---

## Track Templates & Workspace

- **Track Templates (Ctrl+Shift+T)**: Save and recall track configurations with effects chains
- **Track Import**: Import tracks from other projects with all settings
- **Workspace Manager (Ctrl+Shift+W)**: Save and restore window layouts and panel configurations

---

## Documentation


- [CONTRIBUTING Guide](CONTRIBUTING.md) - How to contribute

---

## License

[MusicEngine License (MEL)](LICENSE) - Honor-Based Commercial Support

---

## Links

- [MusicEngine Core](https://github.com/watermann420/MusicEngine)
- [Report Issues](https://github.com/watermann420/MusicEngineEditor/issues)


---
## Free & Open Source Commitment

**MusicEngine**, **MusicEditor**, and the **GameEngine** will always remain  
**free and open source**.

The goal of this project is to build a powerful, accessible ecosystem for music,
audio, and interactive applications — without locking users behind paywalls.

Donations and sponsorships are used to:
- support ongoing development and maintenance
- invest time into improving stability, performance, and documentation
- purchase instruments, hardware, and sound sources
- build a high-quality **free sample library**, including:
- ready-to-use instruments
- pre-configured settings
- samples fully integrated into MusicEngine

All core tools and assets created through this effort will remain freely available
to the community.

This project is driven by openness, long-term sustainability, and shared creativity.

Thank you for supporting independent open-source development ❤️
---
**Music Editor** - Created by Watermann420 and Contributors

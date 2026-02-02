# CLAUDE.md - MusicEngineEditor

This file provides guidance for Claude Code when working on this project.

## Project Overview

MusicEngineEditor is a WPF-based DAW (Digital Audio Workstation) editor built on .NET. It provides visual controls for audio synthesis, effects, mixing, arrangement, and analysis.

## Build & Run

```bash
dotnet build MusicEngineEditor.csproj
dotnet run
```

## Controls Folder Structure

The `Controls/` directory contains WPF UserControls organized by feature domain:

| Folder | Purpose |
|--------|---------|
| `Spatial/` | 3D audio controls: surround panner, binaural renderer, ambisonic encoder/decoder |
| `Analysis/` | Audio analysis tools: tuner, chord/key/tempo detection, loop finder |
| `Network/` | Sync and remote control: Ableton Link, OSC, Network MIDI, Machine Control |
| `Synths/` | Synthesizer type-specific editors: FM, granular, wavetable, vector, etc. |
| `Session/` | Session view and clip launcher controls |
| `Browser/` | File browsers and preset management |
| `Workspace/` | Layout management and workspace presets |
| `AI/` | AI-powered tools: auto-master, auto-mix, melody generator, chord suggester |
| `Effects/` | Effect type editors (dynamics, modulation, distortion, etc.) |
| `Arrangement/` | Timeline and track arrangement controls |
| `PatternEditor/` | Piano roll and MIDI pattern editing |
| `Mixer/` | Channel strips, meters, and mix controls |
| `MIDI/` | MIDI-specific controls and editors |
| `InlineVisuals/` | Code editor inline visualizations |
| `Performance/` | Performance monitoring displays |

## Keyboard Shortcuts

### Panel Access (Function Keys)

| Key | Panel |
|-----|-------|
| F3 | AI Assistant Panel |
| F4 | Synth Editor |
| F5 | Effects Editor |
| F6 | Pattern Editor |
| F7 | Mixer |
| F8 | Arrangement |
| F9 | Performance Monitor |
| F10 | Audio Statistics |

### View Shortcuts (Ctrl+Alt)

| Shortcut | Panel |
|----------|-------|
| Ctrl+Alt+S | Session View |
| Ctrl+Alt+P | Spatial Audio Panel |
| Ctrl+Alt+A | Analysis Panel |
| Ctrl+Alt+N | Network Sync Panel |

### Analysis Tools (Ctrl+Alt)

| Shortcut | Tool |
|----------|------|
| Ctrl+Alt+T | Tuner |
| Ctrl+Alt+C | Chord Detector |
| Ctrl+Alt+K | Key Detector |
| Ctrl+Alt+B | Tempo/BPM Detector |
| Ctrl+Alt+L | Loop Finder |

### Workspace Presets

| Key | Workspace |
|-----|-----------|
| Ctrl+1 | Recording |
| Ctrl+2 | Mixing |
| Ctrl+3 | Mastering |
| Ctrl+4 | Editing |
| Ctrl+5 | Performance |

## Dark Theme Color Scheme

The editor uses a dark theme defined in `Themes/`:

| Purpose | Hex | Usage |
|---------|-----|-------|
| Background | `#0D0D0D` | Main window, deep backgrounds |
| Panel | `#181818` | Panel backgrounds, cards |
| Accent | `#00D9FF` | Selection, focus, primary actions |
| Success | `#00FF88` | Positive feedback, connected states |
| Warning | `#FFB800` | Caution states, pending operations |
| Error | `#FF4757` | Error states, destructive actions |

## Key Services

- `AudioReactiveService` - Processes audio for UI effects (bass/mid/high separation, beat detection)
- `SpatialAudioService` - 3D audio positioning and rendering
- `IntegratedAnalysisService` - Audio analysis (tuning, chords, key, tempo)
- `NetworkSyncService` - Ableton Link, OSC, Network MIDI coordination

## Performance Profiles

Set `ME_PERF_PROFILE` environment variable:
- `low` - Disables MIDI, sequencer, inline visuals, perf monitor
- `balanced` - Default behavior
- `high` - All features enabled, optimized buffer settings

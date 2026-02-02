# MusicEngineEditor

See the root `README.md` for full usage. Highlights of the current editor build:
- Shared audio engine (no double-open MIDI), faster first run.
- Inline visuals and glow for notes/pattern steps and `midi.device(n)` literals (only while events occur).
- Output/Console/Errors tabs: MIDI logs route to Output/Console, errors stay in Errors.
- **Audio-reactive UI**: Run button, sidebar, and background pulse with the music.
- **Colored console output**: Errors (red), warnings (yellow), debug (gray), info (white).

## Audio-Reactive Features

### Audio Reactive Lighting
UI elements respond to audio frequencies in real-time:

```
Run Button     → Bass (20-200Hz) + Beat transients → Glow pulses
Sidebar Icons  → Mids (200-2kHz)                   → Wave effect
Status Light   → Overall RMS                       → Brightness
```

### Audio Visualizer Background
Subtle ambient background (12% max opacity):
- **BassGlow**: Bottom edge, purple/blue gradient
- **MidGlowLeft/Right**: Side edges, cyan gradient
- **HighGlow**: Top edge, white/cyan sparkle
- **AmbientPulse**: Center radial, scales 600-1000px with beat

### Configuration
```csharp
// In MainWindow.xaml.cs
SetAudioVisualizerEnabled(true);      // Toggle on/off
SetAudioVisualizerIntensity(0.12f);   // 0.0 - 0.3 (max 30%)

// AudioReactiveService settings
AudioReactiveService.Instance.Sensitivity = 1.5f;
```

## Performance profiles (modular on/off)
Set environment variables before starting the editor to trim unused systems (handy for game-engine embedding or low-power laptops):

- `ME_PERF_PROFILE=low` disables MIDI, sequencer start, inline visuals, and perf monitor; forces 44.1k/1024 buffers.
- `ME_PERF_PROFILE=balanced` (default) keeps prior behaviour.
- `ME_PERF_PROFILE=high` keeps everything on and nudges 48k/256 buffers.
- Fine-grained overrides: `ME_DISABLE_MIDI=1`, `ME_DISABLE_SEQUENCER=1`, `ME_DISABLE_PERF_MONITOR=1`, `ME_DISABLE_INLINE_VISUALS=1`, `ME_SAMPLE_RATE=48000`, `ME_BUFFER_SIZE=512`.

## New Services

### AudioReactiveService
`Services/AudioReactiveService.cs` - Processes audio for UI effects:
- Subscribes to AnalysisService for spectrum/peak data
- Separates audio into Bass/Mid/High frequency bands
- Beat detection for pulsing effects
- 60 FPS smooth interpolation

### Key Files
- `Services/AudioReactiveService.cs` - Audio processing for UI
- `MainWindow.xaml` - AudioVisualizerCanvas background layer
- `MainWindow.xaml.cs` - UpdateAudioVisualizerBackground()

## Synthesizer Editor

Visual synth editing panel with support for 12 synthesizer types:

### Synth Types
| Type | Description |
|------|-------------|
| Simple | Basic waveform, filter, ADSR |
| Poly | Polyphonic with voice management |
| FM | Frequency modulation synthesis |
| Supersaw | Unison detuned oscillators |
| Advanced | Multi-oscillator with per-osc controls |
| Granular | Grain-based synthesis |
| Sample | Sample playback with zones |
| Speech | Vowel/formant synthesis |
| Physical | Physical modeling (string, wind, bell) |
| Noise | White/pink/brown noise generation |
| Wavetable | Wavetable position morphing |
| Vector | X/Y vector synthesis |

### Opening the Synth Editor
- **F4**: Toggle synth editor panel
- **View menu**: Synth Editor (checkable)
- **Right sidebar**: Synth icon button
- **Right panel**: SYNTH tab
- **Automatic**: Opens when `CreateSynth()` is called in a script

### Synth Dropdown
The synth editor header includes a dropdown to select from all synths created in the current script session. This allows switching between multiple synths for editing.

### Key Files
- `Controls/SynthEditorPanel.xaml` - Main synth editor container
- `Controls/SynthEditorPanel.xaml.cs` - Synth registry and selection logic
- `Controls/Synths/*.xaml` - Individual synth type controls

## DAW Core Features

### Effects Editor (F5)
Visual effect editing panel with support for 8 effect types across 6 categories:

| Category | Effects |
|----------|---------|
| Dynamics | Compressor, Limiter, Gate, Expander |
| Time-Based | Reverb, Delay, Echo |
| Modulation | Chorus, Flanger, Phaser |
| Distortion | Overdrive, Distortion, Fuzz, Bitcrusher |
| Filters | Low Pass, High Pass, Band Pass, Parametric EQ |
| Special | Vocoder, Pitch Shifter, Stereo Widener |

Features:
- Category and effect type dropdowns
- Wet/Dry mix slider
- Bypass toggle
- Effect chain list

### Pattern Editor / Piano Roll (F6)
MIDI pattern editing with full piano roll interface:
- Piano keyboard (C0-B8)
- Grid-based note editing
- Velocity-colored notes (blue=soft, red=loud)
- Grid resolution: 1/4, 1/8, 1/16, 1/32
- Quantize and snap tools
- Click to add, right-click to delete notes

### Mixer Panel (F7)
Professional mixing console:
- Channel strips with volume fader (-60 to +6 dB)
- Stereo VU meters with peak hold
- Pan control (-100L to +100R)
- Solo/Mute buttons
- 4 effect sends (A/B/C/D)
- Master channel with LUFS metering

### Arrangement View (F8)
Timeline-based arrangement editing:
- Track headers with mute/solo
- MIDI and audio clip visualization
- Timeline ruler with bar/beat display
- Playhead and loop region
- Drag to move/resize clips

### Keyboard Shortcuts

#### Quick Access (Function Keys)
| Key | Action |
|-----|--------|
| F4 | Synth Editor |
| F5 | Effects Editor |
| F6 | Pattern Editor |
| F7 | Mixer |
| F8 | Arrangement |
| F9 | Performance Monitor |
| F10 | Audio Statistics |
| F11 | Toggle Fullscreen |
| F12 | Plugin Manager (via command palette) |

#### Editor Shortcuts (Ctrl+Shift)
| Shortcut | Action |
|----------|--------|
| Ctrl+Shift+M | Mixer |
| Ctrl+Shift+P | Pattern Editor |
| Ctrl+Shift+A | Arrangement |
| Ctrl+Shift+E | Effects Editor |
| Ctrl+Shift+Y | Synth Editor |
| Ctrl+Shift+W | Workspace Manager |
| Ctrl+Shift+T | Track Template |
| Ctrl+Shift+R | Render Queue |
| Ctrl+Shift+L | Loudness Report |
| Ctrl+Shift+G | Groove Template |

#### Command Palette & Transport
| Shortcut | Action |
|----------|--------|
| Ctrl+P | Command Palette (fuzzy search for all commands) |
| Ctrl+Enter | Run Script |
| Alt+Space | Panic (All Notes Off) |
| Escape | Stop |

## Analysis Tools

- **Audio Statistics (F10)**: Real-time audio analysis including RMS, peak levels, and frequency spectrum
- **Loudness Report (Ctrl+Shift+L)**: Generate LUFS-compliant loudness reports for mastering
- **Audio Suite**: Comprehensive audio analysis toolkit
- **Project Statistics**: Overview of project resources, track count, and memory usage

## Plugin Management

- **Plugin Manager (F12)**: Browse, install, and manage VST plugins via command palette
- Automatic plugin scanning and validation
- Plugin preset management
- VST2/VST3 support

## Track Templates & Workspace

- **Track Templates (Ctrl+Shift+T)**: Save and recall track configurations with effects chains
- **Track Import**: Import tracks from other projects with all settings
- **Workspace Manager (Ctrl+Shift+W)**: Save and restore window layouts and panel configurations

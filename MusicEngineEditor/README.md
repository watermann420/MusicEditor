# MusicEngineEditor

See the root `README.md` for full usage. Highlights of the current editor build:
- Shared audio engine (no double-open MIDI), faster first run.
- Inline visuals and glow for notes/pattern steps and `midi.device(n)` literals (only while events occur).
- Output/Console/Errors tabs: MIDI logs route to Output/Console, errors stay in Errors.
- **Audio-reactive UI**: Run button, sidebar, and background pulse with the music.
- **Colored console output**: Errors (red), warnings (yellow), debug (gray), info (white).

## New in Feb 2026

### AI Assistant Panel (F3)
Four AI-powered tools in one tabbed panel:
- **Auto-Master**: One-click mastering with LUFS targeting, A/B comparison
- **Auto-Mix**: Frequency collision detection, EQ/compression suggestions
- **Melody Generator**: Scale-aware melody generation with style presets
- **Chord Suggester**: Context-aware suggestions with Roman numeral notation

### Session View & Clip Launcher
- 8x8 grid for Ableton-style clip launching
- Scene triggers for row-based launching
- Visual clip states (empty, loaded, playing, queued)

### Master Channel & Returns
- Master channel strip with LUFS/VU meters
- 4 return tracks (A-D) for auxiliary effects
- Send knobs routing to returns

### Workflow Features
- **Welcome Screen**: Recent projects list with pinning
- **Preset Browser**: Unified browser with search, favorites, tags
- **Undo History**: Visual timeline with jump-to-state
- **Workspace Presets**: Ctrl+1-5 for quick layout switching
- **Track Colors**: 16-color picker with custom palettes
- **Quick Actions**: Customizable toolbar for common tasks
- **Zoom Presets**: 50%-200% quick access
- **Keyboard Shortcuts Editor**: Visual editor with conflict detection

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
| F3 | AI Assistant Panel |
| F4 | Synth Editor |
| F5 | Effects Editor |
| F6 | Pattern Editor |
| F7 | Mixer |
| F8 | Arrangement |
| F9 | Performance Monitor |
| F10 | Audio Statistics |
| F11 | Toggle Fullscreen |
| F12 | Plugin Manager (via command palette) |

#### Workspace Presets
| Key | Workspace |
|-----|-----------|
| Ctrl+1 | Recording |
| Ctrl+2 | Mixing |
| Ctrl+3 | Mastering |
| Ctrl+4 | Editing |
| Ctrl+5 | Performance |

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

## Spatial Audio

### Surround Panner Control
- 2D panner for 5.1/7.1 surround speaker layouts
- Visual speaker positions with source indicator
- LFE level and center divergence controls

### Binaural Renderer Control
- 3D head visualization with rotation tracking
- HRTF profile selection (KEMAR, CIPIC, custom)
- Room simulation with size and damping
- Near-field compensation and crossfeed

### Ambisonic Control
- Ambisonic order selection (1st, 2nd, 3rd order)
- Encoder with azimuth, elevation, distance
- Decoder for various speaker configurations
- Rotation controls (yaw, pitch, roll)

### Key Files
- `Controls/Spatial/SurroundPannerControl.xaml`
- `Controls/Spatial/BinauralRendererControl.xaml`
- `Controls/Spatial/AmbisonicControl.xaml`
- `Controls/Spatial/SpatialAudioPanel.xaml`
- `Services/SpatialAudioService.cs`

## Creative Effects

### AutoTune Control
- Key/scale selector with 15+ scales
- Correction speed and humanize amount
- Formant preservation
- Real-time pitch graph visualization

### BeatRepeat Control
- Grid size selection (1/4, 1/8, 1/16, 1/32)
- 8x16 gate pattern editor
- Decay and pitch shift per repeat
- Stutter mode and probability

### Harmonizer Control
- 4 harmony voice slots
- Interval, detune, level, pan, delay per voice
- Scale lock for diatonic harmonies
- Keyboard visualization of harmonies

### GlitchMachine Control
- 8 toggleable effect modules
- Chaos amount and trigger rate
- 8-step pattern sequencer
- Real-time waveform display

### SpectralFreeze Control
- FFT freeze with 4 snapshot slots
- Morphing between frozen spectrums
- Spectral shift and tilt
- Live/frozen spectrum overlay

### Lo-Fi Effects
| Effect | Features |
|--------|----------|
| TapeStop | Stop/start time, wow/flutter |
| VinylEmulation | Crackle, pops, dust, warp, RPM |
| TapeSaturation | Bias, hiss, tape speed, rolloff |
| Bitcrusher | Bit depth, sample rate, dither |

### Saturation
| Effect | Features |
|--------|----------|
| Saturator | Tube, tape, transistor, digital modes |
| Exciter | Frequency bands, harmonic amount |

## Analysis Panels

### Guitar Tuner Panel
- Circular arc tuner with needle indicator
- Note name, octave, cents deviation
- 6-string guitar visualization
- 10 tuning presets (Standard, Drop D, DADGAD, etc.)
- Strobe tuner mode for precision

### Chord Detector Panel
- Large chord name display
- Piano keyboard with detected notes
- Guitar chord diagrams
- Roman numeral analysis
- Chord history list

### Key Detector Panel
- Circle of fifths visualization
- Chromagram histogram
- Mode detection (Ionian-Locrian)
- Key change timeline
- Relative/parallel key display

### Tempo Detector Panel
- BPM display with tap tempo
- Beat grid visualization
- Time signature detection
- Tempo variation graph
- Half-time/double-time alternatives

### Loop Finder Panel
- Waveform with loop regions
- Loop candidates with similarity scores
- Crossfade preview
- A/B comparison mode
- Export selected loop

### Key Files
- `Controls/Analysis/GuitarTunerPanel.xaml`
- `Controls/Analysis/ChordDetectorPanel.xaml`
- `Controls/Analysis/KeyDetectorPanel.xaml`
- `Controls/Analysis/TempoDetectorPanel.xaml`
- `Controls/Analysis/LoopFinderPanel.xaml`
- `Controls/Analysis/AnalysisPanel.xaml`
- `Services/IntegratedAnalysisService.cs`

## Network/Sync

### Ableton Link (LinkSyncPanel)
- Enable/disable Link synchronization
- Connected peers display
- Session tempo with lock option
- Visual metronome with beat indicators
- Latency compensation

### OSC Control (OSCControlPanel)
- OSC server with configurable ports
- Message monitor with timestamps
- Address mapping with learn mode
- Value range mapping
- Preset save/load

### Network MIDI (NetworkMIDIPanel)
- RTP-MIDI session management
- Bonjour/mDNS discovery
- Session create/join/leave
- Channel filtering (1-16)
- Latency monitoring

### Machine Control (MachineControlPanel)
- MMC (MIDI Machine Control) support
- MTC (MIDI Time Code) generator/receiver
- Timecode display (HH:MM:SS:FF)
- Frame rate selection (24/25/29.97/30)
- Chase lock indicator

### Key Files
- `Controls/Network/LinkSyncPanel.xaml`
- `Controls/Network/OSCControlPanel.xaml`
- `Controls/Network/NetworkMIDIPanel.xaml`
- `Controls/Network/MachineControlPanel.xaml`
- `Controls/Network/NetworkSyncPanel.xaml`
- `Services/NetworkSyncService.cs`

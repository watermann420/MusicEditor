# CLAUDE.md - AI Assistant Context for MusicEngineEditor

This file provides context for AI assistants working on this codebase.

## Project Overview

MusicEngineEditor is a professional WPF-based DAW (Digital Audio Workstation) and code editor for the MusicEngine audio scripting system. It enables music creation through code with real-time visualization, inline parameter controls, and VCV Rack-style modulation.

## Technology Stack

- **Framework**: WPF (.NET 10.0)
- **Language**: C# 13
- **MVVM**: CommunityToolkit.Mvvm (ObservableObject, RelayCommand, ObservableProperty)
- **UI Pattern**: MVVM with code-behind for complex interactions
- **Theme**: Dark theme (#0D0D0D background, #181818 panels, #00D9FF accent)

## Project Structure

```
MusicEngineEditor/
├── Controls/                    # UI Controls (UserControl)
│   ├── Synths/                  # Synthesizer editors (45+ types)
│   ├── Effects/                 # Effect editors (100+ effects)
│   ├── Analysis/                # Analysis tools (tuner, key, tempo, etc.)
│   ├── MIDI/                    # MIDI controls (MPE, expression, probability)
│   ├── Performance/             # Performance tools (looper, DJ, groovebox)
│   ├── Mixer/                   # Mixer controls (channel strip, master, returns)
│   ├── Network/                 # Network sync (Link, OSC, Network MIDI, MMC/MTC)
│   ├── Spatial/                 # Spatial audio (surround, binaural, ambisonics)
│   └── Session/                 # Session view (clip launcher)
├── ViewModels/                  # MVVM ViewModels
│   ├── Synths/                  # Synth ViewModels
│   ├── Effects/                 # Effect ViewModels
│   ├── Analysis/                # Analysis ViewModels
│   └── Network/                 # Network ViewModels
├── Services/                    # Singleton services
├── Views/                       # Windows and dialogs
│   └── Dialogs/                 # Modal dialogs
├── Editor/                      # Code editor components
├── Models/                      # Data models
└── Themes/                      # Dark/Light theme resources
```

## Coding Patterns

### Creating a New Control

1. **XAML file** (`Controls/Category/MyControl.xaml`):
   - Use dark theme colors as StaticResource
   - Define local styles with unique keys (prefix with control name)
   - Follow existing control patterns for layout

2. **Code-behind** (`Controls/Category/MyControl.xaml.cs`):
   - Add license header
   - Use dependency properties for bindable values
   - Create events for parameter changes
   - Implement IDisposable if using timers/subscriptions

3. **ViewModel** (`ViewModels/Category/MyControlViewModel.cs`):
   - Inherit from `ViewModelBase`
   - Use `[ObservableProperty]` for properties
   - Use `[RelayCommand]` for commands
   - Implement IDisposable for cleanup

### Theme Colors

```csharp
// Standard colors (use as StaticResource in XAML)
Background:     #0D0D0D
Panel:          #181818
Accent:         #00D9FF
Text Primary:   #FFFFFF
Text Secondary: #808080
Border:         #2A2A2A
Error:          #FF4757
Warning:        #FFB800
Success:        #00FF88
```

### Common Patterns to Follow

1. **Naming Conflicts**: Use type aliases or full qualification for ambiguous types:
   ```csharp
   using AmbisonicEncoder = MusicEngine.Core.Spatial.AmbisonicEncoder;
   System.Windows.Media.Color.FromRgb(...)
   System.Windows.HorizontalAlignment.Center
   ```

2. **RelayCommand Access**: Commands are private, use `CommandName.Execute(null)` not `MethodName()`

3. **WPF vs CSS**: No `TextTransform` property in WPF - just use uppercase text directly

4. **ToggleButton**: Requires `using System.Windows.Controls.Primitives;`

5. **Custom EventArgs**: Avoid names that conflict with WPF (e.g., use `MusicalKeyEventArgs` not `KeyEventArgs`)

## Key Services

| Service | Purpose |
|---------|---------|
| `IntegratedAnalysisService` | Audio analysis (tuner, chord, key, tempo, loops) |
| `NetworkSyncService` | Network sync (Ableton Link, OSC, Network MIDI, MMC/MTC) |
| `SpatialAudioService` | Spatial audio state management |
| `AudioReactiveService` | UI audio reactivity |
| `ExternalControlService` | Game engine integration |
| `ProjectService` | Project management |

## Feature Summary (as of Feb 2026)

### Synthesizers (Controls/Synths/)
- FM, Granular, Wavetable, Vector, Physical Modeling
- ChipTune (NES/GameBoy/C64), Organ, SID, OPN (Genesis FM)
- EPiano (Rhodes/Wurlitzer), Sampler/Slicer
- Modal, VPM (Phase Distortion), Wavefolder, Subtractive

### Effects (Controls/Effects/)
- **Creative**: AutoTune, BeatRepeat, Harmonizer, GlitchMachine, SpectralFreeze
- **Lo-Fi**: TapeStop, VinylEmulation, TapeSaturation, Bitcrusher
- **Saturation**: Saturator (tube/tape/transistor/digital), Exciter
- **Standard**: Reverb, Delay, Compressor, EQ, Filter, etc.

### Analysis (Controls/Analysis/)
- GuitarTunerPanel (circular tuner, strobe mode, 10 tuning presets)
- ChordDetectorPanel (piano visualization, guitar diagrams, Roman numerals)
- KeyDetectorPanel (circle of fifths, chromagram, mode detection)
- TempoDetectorPanel (BPM, tap tempo, beat grid, time signature)
- LoopFinderPanel (waveform, loop candidates, A/B comparison)

### Spatial Audio (Controls/Spatial/)
- SurroundPannerControl (5.1/7.1 positioning)
- BinauralRendererControl (3D head visualization, HRTF)
- AmbisonicControl (encoder/decoder, rotation)
- SpatialAudioPanel (main container)

### Network/Sync (Controls/Network/)
- LinkSyncPanel (Ableton Link tempo sync)
- OSCControlPanel (OSC server, message mapping)
- NetworkMIDIPanel (RTP-MIDI, session discovery)
- MachineControlPanel (MMC/MTC, timecode)
- NetworkSyncPanel (main container)

## Build Commands

```bash
# Build
dotnet build

# Run
dotnet run --project MusicEngineEditor

# Test
pwsh ./build.ps1 -Clean -Release
```

## Important Notes

1. **Never use `TextTransform`** - it's a CSS property, not WPF
2. **Always qualify ambiguous types** - especially `Color`, `Binding`, `HorizontalAlignment`
3. **Use Commands, not methods** - `[RelayCommand]` generates private methods
4. **Follow the dark theme** - consistency is key
5. **Add license headers** - all files need the MEL license header

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

# MusicEngine Developer Documentation

This folder contains technical documentation for developers integrating MusicEngine into their projects.

## Documentation Index

### Project Structure
- **[ProjectStructure.md](ProjectStructure.md)** - How MusicEngine projects are organized
  - Script files (.me) and namespaces
  - Referencing scripts from other scripts
  - Audio asset management
  - Project references (sharing instruments between projects)
  - Complete examples with drum machines and synthesizers

### External Integration

- **[GameEngineIntegration.md](GameEngineIntegration.md)** - Integrating MusicEngine into game engines
  - ExternalControlService API
  - Built-in variables (BPM, Volume, Intensity, etc.)
  - Built-in events (Play, Stop, TransitionTo, etc.)
  - Unity integration with complete code examples
  - Godot integration (GDScript)
  - Unreal Engine integration (C++)
  - Custom engine integration
  - Music state machine example

- **[AudioStreaming.md](AudioStreaming.md)** - P2P audio streaming protocol
  - Real-time audio communication
  - Raw PCM mode (lowest latency)
  - Commands-only mode (lowest bandwidth)
  - Voice chat application example
  - NAT traversal considerations

- **[ModularEffects.md](ModularEffects.md)** - Code-based modular effects system
- **[InlineVisuals.md](InlineVisuals.md)** - Editor inline visual engine (60 FPS overlays)
  - All available node types
  - Building custom synthesizers
  - Creating effect chains
  - Generative music patches
  - Creating custom nodes
  - Patch save/load system
- **[WhatsNew-Jan2026.md](WhatsNew-Jan2026.md)** - January 2026 changes (glow, MIDI logging, shared engine, random helper).
- **[WhatsNew-Feb2026.md](WhatsNew-Feb2026.md)** - February 2026 UI/UX completion (AI Assistant, Session View, Master Channel, 14 features total).

## Quick Links

### Common Tasks

| Task | Documentation |
|------|---------------|
| Create a new synth | [ModularEffects.md](ModularEffects.md#building-synthesizers) |
| Integrate with Unity | [GameEngineIntegration.md](GameEngineIntegration.md#unity-integration) |
| Stream audio P2P | [AudioStreaming.md](AudioStreaming.md#quick-start) |
| Reference other projects | [ProjectStructure.md](ProjectStructure.md#referencing-other-projects) |
| Build effect chains | [ModularEffects.md](ModularEffects.md#building-effect-chains) |
| Control music from game | [GameEngineIntegration.md](GameEngineIntegration.md#quick-start) |
| MIDI logging & LEDs | See notes below |

### MIDI utilities (engine scripting)

- Logging per device: `midi.device(idx).log.info(true/false)`, `log.cc()`, `log.timingClock()`, `log.screenData()`. Device literal glows on any incoming MIDI when logging/routing is active.
- Transport via CC: `midi.device(idx).cc(ccNumber).toStart()/toStop()/toRefresh()`.
- LEDs (paired MIDI out): `midi.device(idx).led.set(note, value, channel)`, `.off(note)`, `.cc(controller, value)`.
- Pattern with multiple synths: `var pat = CreatePattern(bass, lead, pad);`
- Step shorthand: `pat.Seq("1010x001", opt => opt.pitch(72).velocity(90).step(0.25).duration(0.25));`

### API Reference

| Service | Purpose |
|---------|---------|
| `ExternalControlService` | Variables and events for game integration |
| `AudioStreamingService` | P2P audio/command streaming |
| `EffectGraph` | Modular effect processing |
| `EffectNodeFactory` | Creating effect nodes |
| `EffectPatchManager` | Save/load effect presets |
| `ProjectService` | Project management |
| `SpatialAudioService` | Spatial audio state (surround, binaural, ambisonics) |
| `IntegratedAnalysisService` | Audio analysis (tuner, chord, key, tempo, loops) |
| `NetworkSyncService` | Network sync (Link, OSC, MIDI, MMC/MTC) |
| `AudioReactiveService` | UI audio reactivity |

## Getting Started

### 1. For Game Developers

If you want to add dynamic music to your game:

```csharp
using MusicEngineEditor.Services;

// Get the control service
var control = ExternalControlService.Instance;

// Set music parameters based on game state
control.SetVariable("IntensityLevel", player.DangerLevel);
control.SetVariable("PlayerHealth", player.Health);

// Trigger music events
control.TriggerEvent("TransitionTo", "Combat");
```

See [GameEngineIntegration.md](GameEngineIntegration.md) for full details.

### 2. For Audio Developers

If you want to build custom synths and effects:

```csharp
using MusicEngineEditor.Effects;

// Create effect graph
var graph = new EffectGraph();

// Add oscillator, filter, output
var osc = EffectNodeFactory.CreateNode("Oscillator");
var filter = EffectNodeFactory.CreateNode("Filter");
var output = EffectNodeFactory.CreateNode("AudioOutput");

graph.AddNode(osc);
graph.AddNode(filter);
graph.AddNode(output);

// Connect: Osc -> Filter -> Output
graph.Connect(osc.Id, 0, filter.Id, 0);
graph.Connect(filter.Id, 0, output.Id, 0);

// Process audio
graph.Process(buffer, 512, 44100);
```

See [ModularEffects.md](ModularEffects.md) for full details.

### 3. For Network Audio

If you want to stream audio between computers:

```csharp
using MusicEngineEditor.Services;

var streaming = AudioStreamingService.Instance;

// Start listening
await streaming.StartListeningAsync();

// Connect to peer
var peer = await streaming.ConnectAsync("192.168.1.100", 9000);
await streaming.RequestCallAsync(peer.PeerId);

// Send audio
await streaming.SendAudioAsync(audioBuffer);
```

See [AudioStreaming.md](AudioStreaming.md) for full details.

## Support

- GitHub Issues: https://github.com/watermann420/MusicEngineEditor/issues
- Documentation: https://musicengine.dev/docs

# VST / Plugin Guide (Jan 31, 2026)

## Quick Start
```csharp
// Load by name (searches VST paths)
var vital = vst.load("Vital");
vital?.from(0);       // route MIDI input 0
Sequencer.Start();
```

```csharp
// Load by full path
var diva = vst.loadFile(@"C:\VSTs\Diva.dll");
diva?.from(0);
```

```csharp
// Insert a VST effect after a synth
var synth = CreateSynth();
var verb  = vst.load("ValhallaRoom");
Engine.AddEffectAfter(synth, verb);   // pseudo-helper; use your routing matrix if preferred
```

## Opening the Plugin UI (Editor)
- In the MusicEngineEditor, double‑click the plugin in the VST panel or call `vst.show("Name")`.
- Each plugin opens in its own `VstPluginWindow`; windows are single‑instance per plugin. If a window is already open it is focused instead of recreated.
- Closing the plugin window does **not** unload the plugin; audio keeps playing.
- If a UI stays blank: check DPI/scaling, ensure 64‑bit plugin, try WaveOut/WASAPI instead of ASIO in rare cases.

## MIDI & Audio Routing
- `plugin.from(deviceIndex)` routes a MIDI input to the plugin.
- `Engine.RouteMidiInput(deviceIndex, plugin)` works as well (low‑level).
- Plugins are inserted as instruments (generating audio) or effects (processing audio bus):
  - Instrument: load and route MIDI to it.
  - Effect: call `plugin.insertAfter(source)` or use your routing matrix helpers if present.
- To map CC to parameters: `midi.device(0).cc(74).to(val => plugin.set("cutoff", val));`
- To bypass/enable quickly: `plugin.bypass(true/false);` (if supported by your VstHost implementation).

## Rendering / Bounce
- Real‑time: use `renderRealtime(seconds, "out.wav", RecordingFormat.Wav24Bit)` to capture the master bus including VSTs.
- Pattern‑bound: `renderPatternToFile(pattern, "loop.wav", seconds, RecordingFormat.Wav24Bit)`.
- Offline (faster‑than‑real‑time) bounce is not implemented yet; keep transport running during render.

## CPU / Stability Tips
- Prefer ASIO; next best WASAPI (shared). WaveOut is a fallback.
- Keep only necessary plugin UIs open; some UIs consume extra CPU/GPU.
- If a plugin UI appears blank: try running the editor as a DPI‑aware app and ensure the plugin DLL bitness matches (64‑bit).
- Reduce buffer if UI feels laggy; increase buffer if audio crackles. Typical ASIO target 128–256 samples; WASAPI 10–20 ms.
- Heavy synth stacks: freeze/record to audio using `renderPatternToFile` to save CPU.

## Common Errors & Fixes
- **Plugin not found**: add the path to your VST search paths (see Settings or set `VST_PATHS` env var) or use `vst.loadFile(fullPath)`.
- **No sound**: ensure `from(deviceIndex)` is called and the plugin output is in the routing matrix/mixer.
- **Window won’t open**: the editor allows only one window per plugin; check if it’s behind other windows. If the plugin crashes, remove its DLL from the scan list and restart.
- **Silence after loading**: some plugins need a sample rate match; confirm engine sample rate equals the plugin’s expectation (typically 44.1/48 kHz). Reload plugin after changing sample rate.

## API Reference (core)
- `vst.load(string name)` – loads first match by name from scanned paths.
- `vst.loadFile(string fullPath)` – loads a specific DLL.
- `plugin.from(int midiDeviceIndex)` – route MIDI to plugin.
- `plugin.dispose()` – unloads plugin.
- `vst.show(string name)` – show plugin UI in the editor (if available).
- (If exposed in your build) `plugin.set(string param, float value)` / `plugin.get(string param)` – parameter access.
- (If exposed) `plugin.bypass(bool)` – bypass switch.

## Roadmap
- Offline (non‑real‑time) render path.
- Persisted per‑project VST routing presets.
- Better per‑plugin crash isolation (child process host).
- Full parameter list/automation discovery UI.

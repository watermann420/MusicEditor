# Pattern Rendering & Recording (Jan 2026)

## Quick recipes

### 1) Live MIDI → Pattern (record performance)
```csharp
// Record 6 seconds from MIDI device 0 into a new pattern and route to a synth
var lead = CreateSynth();
var pat = recordMidi(0, 6.0, lead);
pat.Loop = true;
pat.Start();
```

### 2) Bounce a pattern to a file (real‑time)
```csharp
var kick = CreatePattern(synth);
kick.Seq("1000").Start();
renderPatternToFile(kick, "kick_loop.wav", 4.0, RecordingFormat.Wav24Bit);
```

### 3) Capture master output to memory (WAV buffer)
```csharp
var buf = renderToBuffer(5.0, RecordingFormat.Wav24Bit);
File.WriteAllBytes("snapshot.wav", buf);
```

### 4) Quick real-time render of whatever is playing
```csharp
// Capture master bus for 10 seconds to disk (no pattern argument)
renderRealtime(10.0, "live_capture.wav", RecordingFormat.Wav24Bit);
```

## New APIs

- `recordMidi(int deviceIndex, double seconds = 5, ISynth? synth = null) -> Pattern`
  - Listens to raw MIDI NoteOn/Off from the device for the duration, builds a Pattern with proper note lengths.
  - If `synth` is provided, it is played live while recording (and routed automatically).

- `renderPatternToFile(Pattern pattern, string path, double seconds, RecordingFormat format = Wav24Bit)`
  - Real‑time bounce of the master output while the pattern runs.

- `renderToBuffer(double seconds, RecordingFormat format = Wav24Bit) -> byte[]`
  - Captures the master output to an in-memory WAV byte array.
- `renderRealtime(double seconds, string path, RecordingFormat format = Wav24Bit)`
  - Simple “record what you hear now” for a duration.

## Formats

Use `RecordingFormat` enum (existing):
- `Wav16Bit`, `Wav24Bit`, `Wav32BitFloat`
- MP3 via temp‑WAV conversion when recording to disk (not supported for in‑memory).

## Notes & limitations
- Rendering is **real‑time** (no offline faster‑than‑real‑time bounce yet).
- In‑memory recording currently supports WAV only.
- MIDI recording captures NoteOn/Off; CC/aftertouch are ignored for now.
- For flawless captures: stop heavy UI rendering and keep CPU headroom during bounce.

## Roadmap ideas
- Offline (faster‑than‑real‑time) render path using the sequencer without audio hardware.
- Capture CC/automation into patterns.
- Configurable dithering/normalization on export. 

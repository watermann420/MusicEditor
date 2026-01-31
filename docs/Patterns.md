# Patterns: Recording, Rendering, and Playbacks (Jan 2026)

## Recording into Patterns

### Live MIDI → Pattern
```csharp
// Record 6s from MIDI device 0 into a pattern, play the synth live while recording
var lead = CreateSynth();
var pat = recordMidi(0, 6.0, lead);
pat.Loop = true;
pat.Start();
```
- Captures NoteOn/Off with exact lengths.
- Optional synth is routed and played live while recording.

### Capture CC/Automation (roadmap)
- Current capture records notes. Map CC to parameters in your script while recording to imprint the feel; CC-to-pattern lanes will follow in a later drop.

## Rendering / Bouncing

### Real-time bounce to file
```csharp
renderPatternToFile(pat, "loop.wav", 8.0, RecordingFormat.Wav24Bit);
```
- Renders the master bus while the pattern runs (real-time).

### Real-time bounce to memory
```csharp
var bytes = renderToBuffer(4.0, RecordingFormat.Wav24Bit);
File.WriteAllBytes("snapshot.wav", bytes);
```

## Playback tricks

### Looping & Start Offset
```csharp
pat.StartBeat = 4.0;   // start on beat 4
pat.LoopLength = 8.0;  // 8-beat loop
pat.IsLooping = true;
```

### Playback speed / scratch (experimental)
`Pattern.PlaybackSpeed` property is exposed (default 1.0). UI integration and reverse/scratch-safe triggering are WIP; use with care.

## Audio-on-Notes (use samples)
```csharp
var sampler = new SamplePlayer();          // existing sampler instrument
sampler.Load("kick.wav");
var pat = CreatePattern(sampler);
pat.Seq("1000").Start();                   // drum-style sequencing
```

## Render settings & formats
- `RecordingFormat`: `Wav16Bit`, `Wav24Bit`, `Wav32BitFloat`, `Mp3`
- In-memory recording: WAV only (MP3 needs a file path).
- Real-time only (offline faster-than-real-time bounce is on the roadmap).

## Best practices to avoid pops during render
- Keep CPU headroom; close heavy UIs while bouncing.
- Prefer ASIO; next best WASAPI (shared).
- If you hear clicks on long renders, try slightly longer fade via `ClickGuard` settings (engine-level), or shorten render duration to isolate issues.

## Roadmap (planned next)
- Offline/faster-than-real-time bounce.
- CC/automation lanes recorded into patterns.
- Robust scratch/reverse playback tied to `PlaybackSpeed`.
- Per-pattern render directly to file (no live engine required).

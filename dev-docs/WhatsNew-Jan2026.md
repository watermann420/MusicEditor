# What's New (Jan 31, 2026)

Scope: MusicEngine + MusicEngineEditor updates completed in this cycle.

## Live visuals & glow
- Inline glow now covers `Note(...)` pitches, pattern steps, and `midi.device(n)` literals.
- Device literals pulse on **any** incoming MIDI (notes, CC, clock) when routing/logging is active.
- Rendering is restricted to visible, non-comment lines for speed; pulses fade after ~400 ms.

## MIDI logging and LEDs
- `midi.device(x).log.info(true)` logs all inbound data; `.cc()` and `.TimingClock()` split out CC/clock.
- Logs are piped into the editor Output/Console tabs (shared engine instance).
- LED helper: `midi.device(x).led.set(note, val, channel)` plus `.off`/`.cc`; added `test(cycles, delayMs)` for quick light checks.
- `log.screenData()` reports basic screen capability hints for connected controllers.

## Engine/editor integration
- Editor now reuses the singleton engine; no double-opening of MIDI devices. First run is pre-warmed with a tiny script.
- Default startup script is pulled from `MusicEngine/test_script.csx`, keeping editor and engine examples in sync.

## Pattern & sequencing ergonomics
- Patterns can target multiple instruments: `var pat = CreatePattern(synth1, synth2);`
- Step-sequencer shorthand: `pat.Seq("10100101", opt => opt.pitch(72).velocity(90).step(0.25));`
- Random helper: `random.range(0, 2.5)`, `.speed(2)`, works with floats/ints/bools; chainable.

## Error/log UI
- Added dedicated Console tab (ad-hoc script lines via Enter).
- Errors tab uses hint column (basic “did you mean…” suggestions) and keeps build/runtime errors out of Output.

## Notes for contributors
- Inline visuals live in `MusicEngineEditor/Editor/InlineVisualEngine.cs` + `LiveActivityRenderer.cs`.
- MIDI logging lives in `MusicEngine/Core/AudioEngine.cs` and surfaces via `AudioEngineService`.
- Keep new visuals lightweight (no layout thrash) and avoid scanning non-visible text.

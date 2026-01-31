# Inline Visuals (Editor‑only)

This editor feature renders Strudel‑style overlays directly under your code lines and adds live glow feedback for MIDI/notes.

## Commands (write them in code as comments)

- `.punchcard` – shows a punchcard view for current patterns (steps pulse while active).
- `.pianoroll` – shows a compact piano roll with glowing notes while they play.
- `.mixervisual` – shows channel meters (L/R) for the current mixer.

### Options

You can append options after the command (space separated):

- `.mixervisual height=160 color=#0cf`
- `.punchcard scale=1.2`
- `.pianoroll height=140`

If no options are given, sensible defaults are used.

## How to use

1. Add a comment with a command in your script, e.g.
   ```csharp
   // .punchcard
   pattern.Play();
   ```
2. The visual appears directly under that line, pushing following lines visually downward (editor overlay only).
3. Visuals refresh at ~60 FPS for smooth feedback.
4. Live glow also highlights literals in visible lines:
   - `Note(72, …)` pitch numbers glow while that note is sounding.
   - `midi.device(0)` index glows on any incoming MIDI for that device (notes, CC, clock) when routing/logging is active.

## Notes

- These visuals are **editor only** (no impact on engine playback/export).
- Only visible, non‑comment lines are rendered to keep the editor fast.
- If the audio engine is stopped or meters are unavailable, visuals simulate gentle movement so the UI stays alive.
- Remove the command comment to hide the visual.

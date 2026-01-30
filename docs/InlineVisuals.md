# Inline Visuals (Editor‑only)

This editor feature renders Strudel‑style overlays directly under your code lines.

## Commands (write them in code as comments)

- `.punchcard` – shows a punchcard view for current patterns.
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

## Notes

- These visuals are **editor only** (no impact on engine playback/export).
- If the audio engine is stopped or meters are unavailable, visuals simulate gentle movement so the UI stays alive.
- Remove the command comment to hide the visual.

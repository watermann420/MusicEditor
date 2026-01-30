# Inline Visual Engine (Developer Notes)

Location: `MusicEngineEditor/Editor/InlineVisualEngine.cs` + controls under `Controls/InlineVisuals/`.

## Overview

- Scans document lines for commands like `.punchcard`, `.mixervisual`, `.pianoroll`.
- For each command, creates an `InlineVisualHost` bound to that line.
- Hosts are overlaid on top of the `TextView` via a named `Canvas` (`InlineVisualOverlay`).
- Refresh loop runs at 60 FPS using a `DispatcherTimer` (16 ms).
- Note on/off events are forwarded from the attached `Sequencer` to visuals for glow/animation.

## Adding a new visual

1. Create a control in `Controls/InlineVisuals` implementing one or more of:
   - `IAnimatedVisual` (called every frame)
   - `INoteReactive` (receives note on/off)
   - `ISequencerVisual` (gets Sequencer reference)
2. Register command keyword in `InlineVisualEngine` (regex already picks any `.<cmd>`).
3. Map the command to your new control in the `InlineVisualKind` switch.

## Parsing

- Regex: `^\s*//?\s*\.([a-zA-Z]+)(.*)$` — allows `// .mixervisual height=160`.
- Args string is currently passed raw to the control (controls may parse if needed).

## Positioning

- Uses `TextView.GetVisualLine(line)` to compute `VisualTop + Height`, placing overlay directly under the line.
- Width stretches to `TextView.ActualWidth`.
- Requires `TextView.Parent` to be a `Grid`; overlay `Canvas` is added lazily.

## Performance

- 60 FPS timer; visuals should be lightweight (no heavy layout).
- Mixer inline control currently simulates meters if engine meters are unavailable. Hook real meters by implementing `TryGetEngineMeters` in `MixerInlineControl`.

## Sequencer hookup

- `InlineVisualEngine.Sequencer` subscribes to `NoteTriggered`/`NoteEnded` and forwards to hosts.
- Bridge integration: set `inlineVisuals.Sequencer = engine.Sequencer` after engine init (see `MainWindow_Loaded`).

## Future improvements

- Parse args into strongly typed settings (height/color/mode).
- Real meter feed from `ChannelStrip`/`MixerSceneService`.
- Particle/OSC-style visuals can reuse the same host; add new command keyword.

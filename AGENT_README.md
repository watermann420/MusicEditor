# MusicEngine Editor (C++) - Agent Notes

## Vision
- Build a lean, fast editor focused on script-based music creation.
- Use the same Win32 style as the GameEditor for full control.

## Build (Visual Studio / Rider)
- Open `MusicEngineEditor.sln`.
- Project: `MusicEngineEditor` (Win32, no external deps).

## Core Modules (Phase 1)
- App shell: window, input, event loop.
- Editor surface: text editor panel + console output panel.
- Transport: play/stop, BPM display, timing readout.
- File system: open/save project and script files.

## Architectural Guidelines
- Keep UI state in plain structs; render from state every frame.
- Separate engine/runtime logic from UI (no UI dependencies in core).
- Use ASCII-only files.

## Near-Term Tasks
- Implement custom top bar + dock panels like GameEditor.
- Add file tree + output console.
- Hook MusicEngine runtime when ready.

## File Layout
- `MusicEngineEditor/src/main.cpp`: Win32 bootstrap.

## Notes
- SDL/ImGui removed; project is pure Win32 for now.

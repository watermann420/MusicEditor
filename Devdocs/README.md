# Devdocs

## Project Intent
A lightweight, modular code editor for MusicEngine and GameEngine with a focus on live coding.
The UI stays simple, fast, and flexible to let the engine own the heavy lifting.

## Visual Direction
- Dark, minimal, low-gloss UI.
- Clear hierarchy with simple panels.
- No heavy styling or widget overload.

## Architecture Rules
- Keep nesting depth <= 3 levels per module.
- Prefer small, focused files over giant monoliths.
- UI state in plain structs; render from state.
- No blocking I/O on the UI thread.

## Editor Goals
- Live coding friendly (fast run/stop, instant feedback).
- Minimal friction: open file, edit, run.
- Modular panels for future expansion (console, timeline, file tree).

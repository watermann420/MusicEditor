# Status (aktuell)

Diese Datei beschreibt den aktuellen Stand der MusicEngineEditor Integration.
Sie basiert auf dem vorhandenen Code im Repo.

## Editor UI
- Native Win32 UI (C++), fokus auf schnelles Tippen und Live-Run.
- Ein Textfeld als Editor mit Zeilennummern.
- Konsole im unteren Bereich (togglebar), die Engine-Output zeigt.
- Play/Stop Button im Top-Bar.
- Optionaler Text-Glow ueber `MusicEngineEditor/VisualScripts/text_visuals.json`.

## Tastatur
- `Ctrl+Enter`: Refresh (Script neu laden)
- `Esc`: Stop

## Engine Anbindung
- Der Editor sucht nach `MusicEngine.csproj` in diesen Pfaden:
  - aktuelles Verzeichnis
  - `./MusicEngine`
  - `../MusicEngine`
  - `../../MusicEngine`
- `MusicEngine.exe` wird im `MusicEngine/bin` Ordner gesucht (Debug/Release, neueste Datei gewinnt).
- Script-Pfad: `MusicEngine/test_script.csx`
- Start-Command: `MusicEngine.exe --editor`
- Kommunikation: stdin/stdout mit einfachen Commands:
  - `/S` = Refresh
  - `/PLAY` = Start
  - `/STOP` = Sleep
  - `/EXIT` = Stop/Exit

## Script-Workflow
- Beim Start laedt der Editor `test_script.csx` in den Editor.
- Vor Start/Refresh wird das Script gespeichert.
- Refresh sendet `/S` und optional `/PLAY`, falls der Transport aktiv ist.

## Aktuelle Limits
- Keine Projekt-/Dateiverwaltung, es gibt nur ein Script.
- Keine echte Syntax-Highlighting Pipeline (nur Glow/Overlay).
- Editor/Engine sind gekoppelt an lokale Pfade (MusicEngine als Nachbar-Repo).

## Naechste logische Schritte (wenn gewuenscht)
- Script-Switching (mehrere .csx Dateien)
- Saubere Fehlermeldungen, wenn MusicEngine nicht gefunden wird
- Schnellere API-Doku aus der Engine (auto-generiert)

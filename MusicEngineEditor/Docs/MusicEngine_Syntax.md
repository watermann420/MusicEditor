# MusicEngine Script Syntax (Beispiel)

Diese Datei beschreibt, wie der MusicEngineEditor aktuell Scripts laedt und ausfuehrt.
Die Scripts sind C#-Skripte (.csx), die vom MusicEngine Runtime geladen werden.

## Wo liegt das Script?
- Standard-Datei: `MusicEngine/test_script.csx`
- Alternativ wird auch `test_script.csx` im aktuellen Arbeitsordner akzeptiert.

Der Editor laedt diese Datei beim Start, zeigt sie im Editor und speichert sie vor dem Start/Refresh.

## Minimaler Ablauf
1. MusicEngine bauen, so dass `MusicEngine.exe` existiert.
2. Editor starten.
3. Script schreiben/anpassen.
4. Play klicken (oder Refresh mit Ctrl+Enter), um das Script neu zu laden.

## Beispiel-Script
```csharp
// Set BPM and start the transport
Sequencer.Bpm = 120;
Sequencer.Start();

// Create instruments
var bass = CreateSynth();
bass.SetParameter("waveform", 2); // 0=Sine, 1=Square, 2=Saw, 3=Triangle, 4=Noise
bass.SetParameter("cutoff", 0.5f);

var lead = CreateSynth();
lead.SetParameter("waveform", 1);
lead.SetParameter("cutoff", 0.7f);

// Pattern can drive multiple synths
var pat = CreatePattern(bass, lead);

// Add notes (pitch, beat, duration, velocity)
pat.Note(60, 0,   0.5, 100);
pat.Note(64, 0.5, 0.5, 100);
pat.Note(67, 1,   0.5, 100);

// Step-sequencer shorthand (defaults: pitch 60, vel 100, len 0.25 beats)
pat.Seq("10100101", opt => {
    opt.pitch(72).velocity(90).step(0.25).duration(0.25);
});

// Play!
pat.Start();
```

## Notizen
- Syntax und API kommen aus MusicEngine. Falls die Engine aktualisiert wird, kann sich die API aendern.
- Der Editor selbst interpretiert das Script nicht, er sendet es nur an die Engine.

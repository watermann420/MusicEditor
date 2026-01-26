// MusicEngineEditor - Score Note Model
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Defines the duration of a note in standard music notation.
/// </summary>
public enum NoteDuration
{
    /// <summary>Whole note (semibreve) - 4 beats.</summary>
    Whole = 4,

    /// <summary>Half note (minim) - 2 beats.</summary>
    Half = 2,

    /// <summary>Quarter note (crotchet) - 1 beat.</summary>
    Quarter = 1,

    /// <summary>Eighth note (quaver) - 0.5 beats.</summary>
    Eighth = 8,

    /// <summary>Sixteenth note (semiquaver) - 0.25 beats.</summary>
    Sixteenth = 16,

    /// <summary>Thirty-second note (demisemiquaver) - 0.125 beats.</summary>
    ThirtySecond = 32
}

/// <summary>
/// Defines the type of accidental for a note.
/// </summary>
public enum AccidentalType
{
    /// <summary>No accidental.</summary>
    None,

    /// <summary>Sharp - raises pitch by one semitone.</summary>
    Sharp,

    /// <summary>Flat - lowers pitch by one semitone.</summary>
    Flat,

    /// <summary>Natural - cancels previous accidentals.</summary>
    Natural,

    /// <summary>Double sharp - raises pitch by two semitones.</summary>
    DoubleSharp,

    /// <summary>Double flat - lowers pitch by two semitones.</summary>
    DoubleFlat
}

/// <summary>
/// Defines the type of clef used in the staff.
/// </summary>
public enum ClefType
{
    /// <summary>Treble clef (G clef) - middle C is on the first ledger line below the staff.</summary>
    Treble,

    /// <summary>Bass clef (F clef) - middle C is on the first ledger line above the staff.</summary>
    Bass,

    /// <summary>Alto clef (C clef) - middle C is on the middle line of the staff.</summary>
    Alto,

    /// <summary>Tenor clef (C clef) - middle C is on the fourth line of the staff.</summary>
    Tenor
}

/// <summary>
/// Defines the type of articulation for a note.
/// </summary>
public enum ArticulationType
{
    /// <summary>No articulation.</summary>
    None,

    /// <summary>Staccato - short and detached.</summary>
    Staccato,

    /// <summary>Staccatissimo - very short and detached.</summary>
    Staccatissimo,

    /// <summary>Tenuto - held for full duration.</summary>
    Tenuto,

    /// <summary>Accent - emphasized attack.</summary>
    Accent,

    /// <summary>Marcato - strongly accented.</summary>
    Marcato,

    /// <summary>Fermata - held longer than normal duration.</summary>
    Fermata
}

/// <summary>
/// Represents a note in the Score Editor with standard music notation properties.
/// </summary>
public partial class ScoreNote : ObservableObject
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    /// <summary>
    /// Unique identifier for the note.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// MIDI pitch (0-127).
    /// </summary>
    [ObservableProperty]
    private int _midiPitch = 60;

    /// <summary>
    /// Note duration type.
    /// </summary>
    [ObservableProperty]
    private NoteDuration _duration = NoteDuration.Quarter;

    /// <summary>
    /// Measure number (1-based).
    /// </summary>
    [ObservableProperty]
    private int _measure = 1;

    /// <summary>
    /// Position within the measure in beats (0-based).
    /// </summary>
    [ObservableProperty]
    private double _beatPosition;

    /// <summary>
    /// Accidental type for the note.
    /// </summary>
    [ObservableProperty]
    private AccidentalType _accidental = AccidentalType.None;

    /// <summary>
    /// Whether this note is tied to the next note of the same pitch.
    /// </summary>
    [ObservableProperty]
    private bool _isTied;

    /// <summary>
    /// Whether this note is dotted (increases duration by 50%).
    /// </summary>
    [ObservableProperty]
    private bool _isDotted;

    /// <summary>
    /// Voice number for polyphonic notation (1-4).
    /// </summary>
    [ObservableProperty]
    private int _voice = 1;

    /// <summary>
    /// Articulation marking for the note.
    /// </summary>
    [ObservableProperty]
    private ArticulationType _articulation = ArticulationType.None;

    /// <summary>
    /// Note velocity (0-127) for playback.
    /// </summary>
    [ObservableProperty]
    private int _velocity = 100;

    /// <summary>
    /// Whether the note is currently selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Gets the note name with octave (e.g., "C4", "F#3").
    /// </summary>
    public string NoteName => GetNoteName(MidiPitch);

    /// <summary>
    /// Gets the duration in beats based on the Duration property and whether the note is dotted.
    /// </summary>
    public double DurationInBeats
    {
        get
        {
            double baseDuration = Duration switch
            {
                NoteDuration.Whole => 4.0,
                NoteDuration.Half => 2.0,
                NoteDuration.Quarter => 1.0,
                NoteDuration.Eighth => 0.5,
                NoteDuration.Sixteenth => 0.25,
                NoteDuration.ThirtySecond => 0.125,
                _ => 1.0
            };

            return IsDotted ? baseDuration * 1.5 : baseDuration;
        }
    }

    /// <summary>
    /// Gets the total beat position across measures.
    /// </summary>
    /// <param name="beatsPerMeasure">Number of beats per measure (default 4).</param>
    /// <returns>The total beat position.</returns>
    public double GetTotalBeatPosition(int beatsPerMeasure = 4)
    {
        return (Measure - 1) * beatsPerMeasure + BeatPosition;
    }

    /// <summary>
    /// Gets the end beat position.
    /// </summary>
    /// <param name="beatsPerMeasure">Number of beats per measure.</param>
    /// <returns>The end beat position.</returns>
    public double GetEndBeatPosition(int beatsPerMeasure = 4)
    {
        return GetTotalBeatPosition(beatsPerMeasure) + DurationInBeats;
    }

    /// <summary>
    /// Creates a deep copy of this note with a new unique identifier.
    /// </summary>
    /// <returns>A new ScoreNote instance with the same property values but a new Id.</returns>
    public ScoreNote Clone()
    {
        return new ScoreNote
        {
            MidiPitch = MidiPitch,
            Duration = Duration,
            Measure = Measure,
            BeatPosition = BeatPosition,
            Accidental = Accidental,
            IsTied = IsTied,
            IsDotted = IsDotted,
            Voice = Voice,
            Articulation = Articulation,
            Velocity = Velocity,
            IsSelected = false
        };
    }

    /// <summary>
    /// Converts this ScoreNote to a PianoRollNote for MIDI export.
    /// </summary>
    /// <param name="beatsPerMeasure">Number of beats per measure.</param>
    /// <returns>A PianoRollNote representation.</returns>
    public PianoRollNote ToPianoRollNote(int beatsPerMeasure = 4)
    {
        return new PianoRollNote
        {
            Note = MidiPitch,
            StartBeat = GetTotalBeatPosition(beatsPerMeasure),
            Duration = DurationInBeats,
            Velocity = Velocity,
            IsSelected = IsSelected
        };
    }

    /// <summary>
    /// Creates a ScoreNote from a PianoRollNote.
    /// </summary>
    /// <param name="pianoRollNote">The source PianoRollNote.</param>
    /// <param name="beatsPerMeasure">Number of beats per measure.</param>
    /// <returns>A ScoreNote representation.</returns>
    public static ScoreNote FromPianoRollNote(PianoRollNote pianoRollNote, int beatsPerMeasure = 4)
    {
        int measure = (int)(pianoRollNote.StartBeat / beatsPerMeasure) + 1;
        double beatPosition = pianoRollNote.StartBeat % beatsPerMeasure;

        // Determine duration type from the note duration
        NoteDuration duration = pianoRollNote.Duration switch
        {
            >= 3.5 => NoteDuration.Whole,
            >= 1.5 => NoteDuration.Half,
            >= 0.75 => NoteDuration.Quarter,
            >= 0.375 => NoteDuration.Eighth,
            >= 0.1875 => NoteDuration.Sixteenth,
            _ => NoteDuration.ThirtySecond
        };

        // Check if dotted
        bool isDotted = Math.Abs(pianoRollNote.Duration - GetDurationBeats(duration) * 1.5) < 0.01;

        return new ScoreNote
        {
            MidiPitch = pianoRollNote.Note,
            Duration = duration,
            Measure = measure,
            BeatPosition = beatPosition,
            IsDotted = isDotted,
            Velocity = pianoRollNote.Velocity,
            IsSelected = pianoRollNote.IsSelected
        };
    }

    /// <summary>
    /// Gets the duration in beats for a given NoteDuration.
    /// </summary>
    /// <param name="duration">The note duration type.</param>
    /// <returns>The duration in beats.</returns>
    public static double GetDurationBeats(NoteDuration duration)
    {
        return duration switch
        {
            NoteDuration.Whole => 4.0,
            NoteDuration.Half => 2.0,
            NoteDuration.Quarter => 1.0,
            NoteDuration.Eighth => 0.5,
            NoteDuration.Sixteenth => 0.25,
            NoteDuration.ThirtySecond => 0.125,
            _ => 1.0
        };
    }

    /// <summary>
    /// Converts a MIDI note number to a note name with octave.
    /// </summary>
    /// <param name="midiNote">The MIDI note number (0-127).</param>
    /// <returns>The note name with octave (e.g., "C4", "F#3").</returns>
    public static string GetNoteName(int midiNote)
    {
        if (midiNote < 0 || midiNote > 127)
            return "Invalid";

        int noteIndex = midiNote % 12;
        int octave = (midiNote / 12) - 1;
        return $"{NoteNames[noteIndex]}{octave}";
    }

    /// <summary>
    /// Gets the staff line offset from the middle line for a given MIDI pitch and clef.
    /// </summary>
    /// <param name="midiPitch">The MIDI pitch (0-127).</param>
    /// <param name="clef">The clef type.</param>
    /// <returns>The offset from the middle line (positive = above, negative = below).</returns>
    public static int GetStaffLineOffset(int midiPitch, ClefType clef)
    {
        // Middle C (MIDI 60) reference positions for each clef
        // In treble clef, middle C is on the first ledger line below (offset = -6)
        // In bass clef, middle C is on the first ledger line above (offset = 6)
        // In alto clef, middle C is on the middle line (offset = 0)
        // In tenor clef, middle C is on the fourth line (offset = -2)

        int middleCOffset = clef switch
        {
            ClefType.Treble => -6,
            ClefType.Bass => 6,
            ClefType.Alto => 0,
            ClefType.Tenor => -2,
            _ => -6
        };

        // Calculate the offset from middle C in half steps
        int halfStepsFromMiddleC = midiPitch - 60;

        // Convert half steps to staff positions (diatonic scale)
        // The pattern repeats every 12 half steps (octave) = 7 staff positions
        int octaveOffset = halfStepsFromMiddleC / 12;
        int noteWithinOctave = ((halfStepsFromMiddleC % 12) + 12) % 12;

        // Map chromatic to diatonic (0-11 to 0-6 with adjustments for accidentals)
        int[] chromaticToDiatonic = { 0, 0, 1, 1, 2, 3, 3, 4, 4, 5, 5, 6 };
        int diatonicOffset = chromaticToDiatonic[noteWithinOctave];

        return middleCOffset + (octaveOffset * 7) + diatonicOffset;
    }

    /// <summary>
    /// Determines if a pitch requires an accidental based on the key signature.
    /// </summary>
    /// <param name="midiPitch">The MIDI pitch.</param>
    /// <param name="keySignature">The key signature (e.g., "C", "G", "F", "Am").</param>
    /// <returns>The required accidental type, or None if no accidental is needed.</returns>
    public static AccidentalType GetRequiredAccidental(int midiPitch, string keySignature)
    {
        int noteInOctave = midiPitch % 12;

        // Sharps in key: F#, C#, G#, D#, A#, E#, B#
        // Flats in key: Bb, Eb, Ab, Db, Gb, Cb, Fb

        // Get the sharps or flats in the key signature
        int[] sharpsInKey = GetSharpsInKey(keySignature);
        int[] flatsInKey = GetFlatsInKey(keySignature);

        // Check if the note is naturally in the key
        bool isSharpNote = noteInOctave is 1 or 3 or 6 or 8 or 10;
        bool needsSharp = Array.IndexOf(sharpsInKey, noteInOctave) >= 0;
        bool needsFlat = Array.IndexOf(flatsInKey, noteInOctave) >= 0;

        if (isSharpNote && !needsSharp && !needsFlat)
        {
            // The pitch is a sharp/flat but not in the key signature
            return AccidentalType.Sharp; // or Flat depending on context
        }

        return AccidentalType.None;
    }

    private static int[] GetSharpsInKey(string key)
    {
        return key.ToUpperInvariant() switch
        {
            "G" or "EM" => [6],                    // F#
            "D" or "BM" => [6, 1],                 // F#, C#
            "A" or "F#M" => [6, 1, 8],             // F#, C#, G#
            "E" or "C#M" => [6, 1, 8, 3],          // F#, C#, G#, D#
            "B" or "G#M" => [6, 1, 8, 3, 10],      // F#, C#, G#, D#, A#
            "F#" or "D#M" => [6, 1, 8, 3, 10, 5],  // F#, C#, G#, D#, A#, E#
            "C#" or "A#M" => [6, 1, 8, 3, 10, 5, 0], // All sharps
            _ => []
        };
    }

    private static int[] GetFlatsInKey(string key)
    {
        return key.ToUpperInvariant() switch
        {
            "F" or "DM" => [10],                   // Bb
            "BB" or "GM" => [10, 3],               // Bb, Eb
            "EB" or "CM" => [10, 3, 8],            // Bb, Eb, Ab
            "AB" or "FM" => [10, 3, 8, 1],         // Bb, Eb, Ab, Db
            "DB" or "BBM" => [10, 3, 8, 1, 6],     // Bb, Eb, Ab, Db, Gb
            "GB" or "EBM" => [10, 3, 8, 1, 6, 11], // Bb, Eb, Ab, Db, Gb, Cb
            "CB" or "ABM" => [10, 3, 8, 1, 6, 11, 4], // All flats
            _ => []
        };
    }

    partial void OnMidiPitchChanged(int value)
    {
        OnPropertyChanged(nameof(NoteName));
        OnPropertyChanged(nameof(DurationInBeats));
    }

    partial void OnDurationChanged(NoteDuration value)
    {
        OnPropertyChanged(nameof(DurationInBeats));
    }

    partial void OnIsDottedChanged(bool value)
    {
        OnPropertyChanged(nameof(DurationInBeats));
    }
}

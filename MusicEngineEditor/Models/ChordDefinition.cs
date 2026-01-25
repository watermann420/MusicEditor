// MusicEngineEditor - Chord Definition Model
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Description: Chord definition for piano roll chord stamping with built-in chords, inversions, and voicings.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngineEditor.Models;

/// <summary>
/// Chord voicing types for different note arrangements.
/// </summary>
public enum ChordVoicing
{
    /// <summary>
    /// Close position - notes stacked in the smallest interval.
    /// </summary>
    Close,

    /// <summary>
    /// Open position - notes spread across octaves.
    /// </summary>
    Open,

    /// <summary>
    /// Drop 2 voicing - second voice from top dropped an octave.
    /// </summary>
    Drop2,

    /// <summary>
    /// Drop 3 voicing - third voice from top dropped an octave.
    /// </summary>
    Drop3
}

/// <summary>
/// Represents a musical chord definition with intervals, inversions, and voicings.
/// Used for chord stamping in the Piano Roll editor.
/// </summary>
public sealed class ChordDefinition
{
    #region Built-in Chords

    /// <summary>
    /// Major triad.
    /// </summary>
    public static readonly ChordDefinition Major = new("Maj", "Major", 0, 4, 7);

    /// <summary>
    /// Minor triad.
    /// </summary>
    public static readonly ChordDefinition Minor = new("Min", "Minor", 0, 3, 7);

    /// <summary>
    /// Diminished triad.
    /// </summary>
    public static readonly ChordDefinition Diminished = new("Dim", "Diminished", 0, 3, 6);

    /// <summary>
    /// Augmented triad.
    /// </summary>
    public static readonly ChordDefinition Augmented = new("Aug", "Augmented", 0, 4, 8);

    /// <summary>
    /// Suspended second.
    /// </summary>
    public static readonly ChordDefinition Sus2 = new("Sus2", "Suspended 2nd", 0, 2, 7);

    /// <summary>
    /// Suspended fourth.
    /// </summary>
    public static readonly ChordDefinition Sus4 = new("Sus4", "Suspended 4th", 0, 5, 7);

    /// <summary>
    /// Dominant seventh.
    /// </summary>
    public static readonly ChordDefinition Dom7 = new("7", "Dominant 7th", 0, 4, 7, 10);

    /// <summary>
    /// Major seventh.
    /// </summary>
    public static readonly ChordDefinition Maj7 = new("Maj7", "Major 7th", 0, 4, 7, 11);

    /// <summary>
    /// Minor seventh.
    /// </summary>
    public static readonly ChordDefinition Min7 = new("Min7", "Minor 7th", 0, 3, 7, 10);

    /// <summary>
    /// Diminished seventh.
    /// </summary>
    public static readonly ChordDefinition Dim7 = new("Dim7", "Diminished 7th", 0, 3, 6, 9);

    /// <summary>
    /// Half-diminished seventh (minor 7 flat 5).
    /// </summary>
    public static readonly ChordDefinition Min7b5 = new("m7b5", "Half-Diminished", 0, 3, 6, 10);

    /// <summary>
    /// Augmented seventh.
    /// </summary>
    public static readonly ChordDefinition Aug7 = new("Aug7", "Augmented 7th", 0, 4, 8, 10);

    /// <summary>
    /// Major sixth.
    /// </summary>
    public static readonly ChordDefinition Maj6 = new("6", "Major 6th", 0, 4, 7, 9);

    /// <summary>
    /// Minor sixth.
    /// </summary>
    public static readonly ChordDefinition Min6 = new("m6", "Minor 6th", 0, 3, 7, 9);

    /// <summary>
    /// Dominant ninth.
    /// </summary>
    public static readonly ChordDefinition Dom9 = new("9", "Dominant 9th", 0, 4, 7, 10, 14);

    /// <summary>
    /// Major ninth.
    /// </summary>
    public static readonly ChordDefinition Maj9 = new("Maj9", "Major 9th", 0, 4, 7, 11, 14);

    /// <summary>
    /// Minor ninth.
    /// </summary>
    public static readonly ChordDefinition Min9 = new("Min9", "Minor 9th", 0, 3, 7, 10, 14);

    /// <summary>
    /// Dominant eleventh.
    /// </summary>
    public static readonly ChordDefinition Dom11 = new("11", "Dominant 11th", 0, 4, 7, 10, 14, 17);

    /// <summary>
    /// Dominant thirteenth.
    /// </summary>
    public static readonly ChordDefinition Dom13 = new("13", "Dominant 13th", 0, 4, 7, 10, 14, 21);

    /// <summary>
    /// Add nine.
    /// </summary>
    public static readonly ChordDefinition Add9 = new("add9", "Add 9", 0, 4, 7, 14);

    /// <summary>
    /// Add eleven.
    /// </summary>
    public static readonly ChordDefinition Add11 = new("add11", "Add 11", 0, 4, 7, 17);

    /// <summary>
    /// Power chord (fifth).
    /// </summary>
    public static readonly ChordDefinition Power = new("5", "Power", 0, 7);

    /// <summary>
    /// Minor major seventh.
    /// </summary>
    public static readonly ChordDefinition MinMaj7 = new("mMaj7", "Minor Major 7th", 0, 3, 7, 11);

    /// <summary>
    /// All built-in chords grouped by category.
    /// </summary>
    public static readonly IReadOnlyList<ChordDefinition> AllChords = new[]
    {
        // Basic triads
        Major, Minor, Diminished, Augmented, Sus2, Sus4, Power,
        // Seventh chords
        Dom7, Maj7, Min7, Dim7, Min7b5, Aug7, MinMaj7,
        // Sixth chords
        Maj6, Min6,
        // Extended chords
        Dom9, Maj9, Min9, Dom11, Dom13, Add9, Add11
    };

    /// <summary>
    /// Chord categories for UI grouping.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, IReadOnlyList<ChordDefinition>> ChordsByCategory = new Dictionary<string, IReadOnlyList<ChordDefinition>>
    {
        ["Triads"] = new[] { Major, Minor, Diminished, Augmented, Sus2, Sus4, Power },
        ["7th Chords"] = new[] { Dom7, Maj7, Min7, Dim7, Min7b5, Aug7, MinMaj7 },
        ["6th Chords"] = new[] { Maj6, Min6 },
        ["Extended"] = new[] { Dom9, Maj9, Min9, Dom11, Dom13, Add9, Add11 }
    };

    #endregion

    #region Properties

    /// <summary>
    /// Gets the short symbol for the chord (e.g., "Maj", "Min7").
    /// </summary>
    public string Symbol { get; }

    /// <summary>
    /// Gets the full name of the chord.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the intervals from the root note (semitones).
    /// </summary>
    public IReadOnlyList<int> Intervals { get; }

    /// <summary>
    /// Gets the number of notes in the chord.
    /// </summary>
    public int NoteCount => Intervals.Count;

    /// <summary>
    /// Gets the maximum inversion number (NoteCount - 1).
    /// </summary>
    public int MaxInversion => NoteCount - 1;

    #endregion

    #region Static Properties

    /// <summary>
    /// Note names for display.
    /// </summary>
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    /// <summary>
    /// Inversion names.
    /// </summary>
    private static readonly string[] InversionNames = { "Root", "1st", "2nd", "3rd", "4th", "5th", "6th" };

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new chord definition.
    /// </summary>
    /// <param name="symbol">Short symbol (e.g., "Maj7").</param>
    /// <param name="name">Full name (e.g., "Major Seventh").</param>
    /// <param name="intervals">Intervals from root in semitones.</param>
    public ChordDefinition(string symbol, string name, params int[] intervals)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentNullException(nameof(symbol));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        if (intervals == null || intervals.Length < 2)
            throw new ArgumentException("Chord must have at least 2 intervals.", nameof(intervals));

        Symbol = symbol;
        Name = name;
        Intervals = intervals.OrderBy(i => i).ToArray();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets the MIDI note numbers for this chord.
    /// </summary>
    /// <param name="rootNote">Root MIDI note (0-127).</param>
    /// <param name="inversion">Inversion number (0 = root, 1 = 1st, etc.).</param>
    /// <param name="voicing">Voicing type.</param>
    /// <returns>Array of MIDI note numbers for the chord.</returns>
    public int[] GetNotes(int rootNote, int inversion = 0, ChordVoicing voicing = ChordVoicing.Close)
    {
        // Start with base intervals
        var notes = Intervals.Select(i => rootNote + i).ToList();

        // Apply inversion
        inversion = Math.Clamp(inversion, 0, MaxInversion);
        for (int i = 0; i < inversion; i++)
        {
            // Move the lowest note up an octave
            notes[i] += 12;
        }

        // Sort notes
        notes.Sort();

        // Apply voicing
        notes = ApplyVoicing(notes, voicing);

        // Clamp to valid MIDI range
        return notes.Select(n => Math.Clamp(n, 0, 127)).ToArray();
    }

    /// <summary>
    /// Gets the display name for an inversion.
    /// </summary>
    /// <param name="inversion">Inversion number (0-6).</param>
    /// <returns>Inversion name (e.g., "Root", "1st", "2nd").</returns>
    public static string GetInversionName(int inversion)
    {
        if (inversion < 0 || inversion >= InversionNames.Length)
            return inversion.ToString();

        return InversionNames[inversion];
    }

    /// <summary>
    /// Gets all available inversions for this chord.
    /// </summary>
    /// <returns>List of (inversion number, name) tuples.</returns>
    public IReadOnlyList<(int Number, string Name)> GetAvailableInversions()
    {
        return Enumerable.Range(0, NoteCount)
            .Select(i => (i, GetInversionName(i)))
            .ToArray();
    }

    /// <summary>
    /// Gets the full chord name with root note.
    /// </summary>
    /// <param name="rootNote">Root note (0-11).</param>
    /// <returns>Full chord name (e.g., "C Major", "F# Minor 7th").</returns>
    public string GetFullName(int rootNote)
    {
        return $"{NoteNames[rootNote % 12]} {Name}";
    }

    /// <summary>
    /// Gets the chord symbol with root note.
    /// </summary>
    /// <param name="rootNote">Root note (0-11).</param>
    /// <returns>Chord symbol (e.g., "CMaj", "F#m7").</returns>
    public string GetSymbolWithRoot(int rootNote)
    {
        return $"{NoteNames[rootNote % 12]}{Symbol}";
    }

    /// <summary>
    /// Gets the name of a root note.
    /// </summary>
    /// <param name="rootNote">Root note (0-11 or MIDI note).</param>
    /// <returns>Note name (e.g., "C", "F#").</returns>
    public static string GetNoteName(int rootNote)
    {
        return NoteNames[((rootNote % 12) + 12) % 12];
    }

    /// <summary>
    /// Gets all root note options for UI.
    /// </summary>
    /// <returns>Array of (noteNumber, noteName) tuples.</returns>
    public static IReadOnlyList<(int NoteNumber, string NoteName)> GetAllRootNotes()
    {
        return Enumerable.Range(0, 12)
            .Select(i => (i, NoteNames[i]))
            .ToArray();
    }

    /// <summary>
    /// Gets all voicing options for UI.
    /// </summary>
    /// <returns>Array of ChordVoicing values with display names.</returns>
    public static IReadOnlyList<(ChordVoicing Voicing, string Name)> GetAllVoicings()
    {
        return new[]
        {
            (ChordVoicing.Close, "Close"),
            (ChordVoicing.Open, "Open"),
            (ChordVoicing.Drop2, "Drop 2"),
            (ChordVoicing.Drop3, "Drop 3")
        };
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Applies a voicing transformation to the chord notes.
    /// </summary>
    private static List<int> ApplyVoicing(List<int> notes, ChordVoicing voicing)
    {
        if (notes.Count < 3)
            return notes; // Can't apply advanced voicings to small chords

        switch (voicing)
        {
            case ChordVoicing.Open:
                // Spread notes across two octaves
                var openNotes = new List<int>();
                for (int i = 0; i < notes.Count; i++)
                {
                    // Alternate notes between octaves
                    openNotes.Add(notes[i] + (i % 2 == 0 ? 0 : 12));
                }
                openNotes.Sort();
                return openNotes;

            case ChordVoicing.Drop2:
                // Drop the second voice from the top by an octave
                if (notes.Count >= 3)
                {
                    var drop2Notes = new List<int>(notes);
                    int secondFromTop = drop2Notes.Count - 2;
                    drop2Notes[secondFromTop] -= 12;
                    drop2Notes.Sort();
                    return drop2Notes;
                }
                break;

            case ChordVoicing.Drop3:
                // Drop the third voice from the top by an octave
                if (notes.Count >= 4)
                {
                    var drop3Notes = new List<int>(notes);
                    int thirdFromTop = drop3Notes.Count - 3;
                    drop3Notes[thirdFromTop] -= 12;
                    drop3Notes.Sort();
                    return drop3Notes;
                }
                break;
        }

        return notes; // Close voicing or fallback
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Returns the chord symbol.
    /// </summary>
    public override string ToString() => Symbol;

    /// <summary>
    /// Checks equality based on symbol and intervals.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not ChordDefinition other)
            return false;

        return Symbol == other.Symbol && Intervals.SequenceEqual(other.Intervals);
    }

    /// <summary>
    /// Gets hash code based on symbol.
    /// </summary>
    public override int GetHashCode() => Symbol.GetHashCode();

    #endregion
}

// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Data model class.

using System;
using System.Collections.Generic;
using System.Linq;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a musical scale definition with name and intervals.
/// Used for scale highlighting in the Piano Roll editor.
/// </summary>
public sealed class ScaleDefinition
{
    #region Built-in Scales

    /// <summary>
    /// Major scale (Ionian mode).
    /// </summary>
    public static readonly ScaleDefinition Major = new("Major", 0, 2, 4, 5, 7, 9, 11);

    /// <summary>
    /// Natural Minor scale (Aeolian mode).
    /// </summary>
    public static readonly ScaleDefinition Minor = new("Minor", 0, 2, 3, 5, 7, 8, 10);

    /// <summary>
    /// Dorian mode.
    /// </summary>
    public static readonly ScaleDefinition Dorian = new("Dorian", 0, 2, 3, 5, 7, 9, 10);

    /// <summary>
    /// Phrygian mode.
    /// </summary>
    public static readonly ScaleDefinition Phrygian = new("Phrygian", 0, 1, 3, 5, 7, 8, 10);

    /// <summary>
    /// Lydian mode.
    /// </summary>
    public static readonly ScaleDefinition Lydian = new("Lydian", 0, 2, 4, 6, 7, 9, 11);

    /// <summary>
    /// Mixolydian mode.
    /// </summary>
    public static readonly ScaleDefinition Mixolydian = new("Mixolydian", 0, 2, 4, 5, 7, 9, 10);

    /// <summary>
    /// Locrian mode.
    /// </summary>
    public static readonly ScaleDefinition Locrian = new("Locrian", 0, 1, 3, 5, 6, 8, 10);

    /// <summary>
    /// Harmonic Minor scale.
    /// </summary>
    public static readonly ScaleDefinition HarmonicMinor = new("Harmonic Minor", 0, 2, 3, 5, 7, 8, 11);

    /// <summary>
    /// Melodic Minor scale (ascending).
    /// </summary>
    public static readonly ScaleDefinition MelodicMinor = new("Melodic Minor", 0, 2, 3, 5, 7, 9, 11);

    /// <summary>
    /// Pentatonic Major scale.
    /// </summary>
    public static readonly ScaleDefinition PentatonicMajor = new("Pentatonic Major", 0, 2, 4, 7, 9);

    /// <summary>
    /// Pentatonic Minor scale.
    /// </summary>
    public static readonly ScaleDefinition PentatonicMinor = new("Pentatonic Minor", 0, 3, 5, 7, 10);

    /// <summary>
    /// Blues scale.
    /// </summary>
    public static readonly ScaleDefinition Blues = new("Blues", 0, 3, 5, 6, 7, 10);

    /// <summary>
    /// Whole Tone scale.
    /// </summary>
    public static readonly ScaleDefinition WholeTone = new("Whole Tone", 0, 2, 4, 6, 8, 10);

    /// <summary>
    /// Chromatic scale (all 12 semitones).
    /// </summary>
    public static readonly ScaleDefinition Chromatic = new("Chromatic", 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11);

    /// <summary>
    /// Hungarian Minor scale.
    /// </summary>
    public static readonly ScaleDefinition HungarianMinor = new("Hungarian Minor", 0, 2, 3, 6, 7, 8, 11);

    /// <summary>
    /// Spanish/Phrygian Dominant scale.
    /// </summary>
    public static readonly ScaleDefinition Spanish = new("Spanish", 0, 1, 4, 5, 7, 8, 10);

    /// <summary>
    /// Japanese Hirajoshi scale.
    /// </summary>
    public static readonly ScaleDefinition Japanese = new("Japanese (Hirajoshi)", 0, 2, 3, 7, 8);

    /// <summary>
    /// Arabic scale.
    /// </summary>
    public static readonly ScaleDefinition Arabic = new("Arabic", 0, 1, 4, 5, 7, 8, 11);

    /// <summary>
    /// Diminished scale (half-whole).
    /// </summary>
    public static readonly ScaleDefinition Diminished = new("Diminished", 0, 1, 3, 4, 6, 7, 9, 10);

    /// <summary>
    /// All built-in scales.
    /// </summary>
    public static readonly IReadOnlyList<ScaleDefinition> AllScales = new[]
    {
        Major, Minor, Dorian, Phrygian, Lydian, Mixolydian, Locrian,
        HarmonicMinor, MelodicMinor, PentatonicMajor, PentatonicMinor,
        Blues, WholeTone, Chromatic, HungarianMinor, Spanish, Japanese, Arabic, Diminished
    };

    #endregion

    #region Properties

    /// <summary>
    /// Gets the name of the scale.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the intervals from the root note (semitones).
    /// </summary>
    public IReadOnlyList<int> Intervals { get; }

    /// <summary>
    /// Gets the number of notes in the scale.
    /// </summary>
    public int NoteCount => Intervals.Count;

    #endregion

    #region Static Properties

    /// <summary>
    /// Note names for display.
    /// </summary>
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    /// <summary>
    /// Scale degree names.
    /// </summary>
    private static readonly string[] ScaleDegreeNames = { "1", "2", "3", "4", "5", "6", "7" };

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new scale definition.
    /// </summary>
    /// <param name="name">Name of the scale.</param>
    /// <param name="intervals">Intervals from root in semitones (first should be 0).</param>
    public ScaleDefinition(string name, params int[] intervals)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentNullException(nameof(name));

        if (intervals == null || intervals.Length == 0)
            throw new ArgumentException("Scale must have at least one interval.", nameof(intervals));

        Name = name;
        Intervals = intervals.OrderBy(i => i).Distinct().ToArray();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Gets all MIDI note numbers in this scale for a given root note.
    /// </summary>
    /// <param name="rootNote">Root note (0-11, where 0 = C).</param>
    /// <param name="minNote">Minimum MIDI note (default 0).</param>
    /// <param name="maxNote">Maximum MIDI note (default 127).</param>
    /// <returns>Array of MIDI note numbers in the scale.</returns>
    public int[] GetNotesInScale(int rootNote, int minNote = 0, int maxNote = 127)
    {
        rootNote = NormalizeNote(rootNote);
        var notes = new List<int>();

        for (int octave = -1; octave <= 10; octave++)
        {
            foreach (var interval in Intervals)
            {
                int midiNote = rootNote + interval + (octave * 12);
                if (midiNote >= minNote && midiNote <= maxNote)
                {
                    notes.Add(midiNote);
                }
            }
        }

        return notes.OrderBy(n => n).ToArray();
    }

    /// <summary>
    /// Checks if a MIDI note is in this scale for a given root.
    /// </summary>
    /// <param name="midiNote">The MIDI note number (0-127).</param>
    /// <param name="rootNote">Root note (0-11, where 0 = C).</param>
    /// <returns>True if the note is in the scale.</returns>
    public bool IsNoteInScale(int midiNote, int rootNote)
    {
        rootNote = NormalizeNote(rootNote);
        int pitchClass = NormalizeNote(midiNote);
        int interval = (pitchClass - rootNote + 12) % 12;
        return Intervals.Contains(interval);
    }

    /// <summary>
    /// Gets the scale degree of a note (1-based, or 0 if not in scale).
    /// </summary>
    /// <param name="midiNote">The MIDI note number.</param>
    /// <param name="rootNote">Root note (0-11, where 0 = C).</param>
    /// <returns>Scale degree (1-7 or more), or 0 if not in scale.</returns>
    public int GetScaleDegree(int midiNote, int rootNote)
    {
        rootNote = NormalizeNote(rootNote);
        int pitchClass = NormalizeNote(midiNote);
        int interval = (pitchClass - rootNote + 12) % 12;

        for (int i = 0; i < Intervals.Count; i++)
        {
            if (Intervals[i] == interval)
            {
                return i + 1;
            }
        }

        return 0; // Not in scale
    }

    /// <summary>
    /// Gets the scale degree label for a note.
    /// </summary>
    /// <param name="midiNote">The MIDI note number.</param>
    /// <param name="rootNote">Root note (0-11, where 0 = C).</param>
    /// <returns>Scale degree label (e.g., "1", "2", etc.) or empty if not in scale.</returns>
    public string GetScaleDegreeLabel(int midiNote, int rootNote)
    {
        int degree = GetScaleDegree(midiNote, rootNote);
        if (degree == 0)
            return string.Empty;

        return degree.ToString();
    }

    /// <summary>
    /// Gets the name of a root note.
    /// </summary>
    /// <param name="rootNote">Root note (0-11).</param>
    /// <returns>Note name (e.g., "C", "F#").</returns>
    public static string GetNoteName(int rootNote)
    {
        return NoteNames[NormalizeNote(rootNote)];
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
    /// Creates a scale map (12-element boolean array) for efficient lookup.
    /// </summary>
    /// <param name="rootNote">Root note (0-11).</param>
    /// <returns>Boolean array where true indicates a scale tone.</returns>
    public bool[] CreateScaleMap(int rootNote)
    {
        var map = new bool[12];
        rootNote = NormalizeNote(rootNote);

        foreach (var interval in Intervals)
        {
            int pitchClass = (rootNote + interval) % 12;
            map[pitchClass] = true;
        }

        return map;
    }

    #endregion

    #region Private Methods

    /// <summary>
    /// Normalizes a note to 0-11 range.
    /// </summary>
    private static int NormalizeNote(int note)
    {
        return ((note % 12) + 12) % 12;
    }

    #endregion

    #region Overrides

    /// <summary>
    /// Returns the scale name.
    /// </summary>
    public override string ToString() => Name;

    /// <summary>
    /// Checks equality based on name and intervals.
    /// </summary>
    public override bool Equals(object? obj)
    {
        if (obj is not ScaleDefinition other)
            return false;

        return Name == other.Name && Intervals.SequenceEqual(other.Intervals);
    }

    /// <summary>
    /// Gets hash code based on name.
    /// </summary>
    public override int GetHashCode() => Name.GetHashCode();

    #endregion
}

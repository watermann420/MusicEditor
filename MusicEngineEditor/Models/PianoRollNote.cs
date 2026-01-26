// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Piano roll note data model.

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a note in the Piano Roll editor.
/// </summary>
public partial class PianoRollNote : ObservableObject
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private static readonly bool[] BlackKeyPattern = { false, true, false, true, false, false, true, false, true, false, true, false };

    /// <summary>
    /// Unique identifier for the note.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// MIDI note number (0-127).
    /// </summary>
    [ObservableProperty]
    private int _note;

    /// <summary>
    /// Start position in beats.
    /// </summary>
    [ObservableProperty]
    private double _startBeat;

    /// <summary>
    /// Length of the note in beats.
    /// </summary>
    [ObservableProperty]
    private double _duration = 1.0;

    /// <summary>
    /// Note velocity (0-127).
    /// </summary>
    [ObservableProperty]
    private int _velocity = 100;

    /// <summary>
    /// Indicates whether the note is currently selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Indicates whether the note is currently being played.
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// MIDI channel (0-15).
    /// </summary>
    [ObservableProperty]
    private int _channel;

    /// <summary>
    /// Hex color for display (e.g., "#FF5722").
    /// </summary>
    [ObservableProperty]
    private string _color = "#4CAF50";

    /// <summary>
    /// Gets the note name with octave (e.g., "C4", "F#3").
    /// </summary>
    public string NoteName => GetNoteName(Note);

    /// <summary>
    /// Called when the Note property changes to notify NoteName has also changed.
    /// </summary>
    partial void OnNoteChanged(int value)
    {
        OnPropertyChanged(nameof(NoteName));
    }

    /// <summary>
    /// Gets the end beat position (StartBeat + Duration).
    /// </summary>
    /// <returns>The end beat position.</returns>
    public double GetEndBeat() => StartBeat + Duration;

    /// <summary>
    /// Checks if this note overlaps with the specified beat range.
    /// </summary>
    /// <param name="startBeat">Start of the range in beats.</param>
    /// <param name="endBeat">End of the range in beats.</param>
    /// <returns>True if the note overlaps with the range; otherwise, false.</returns>
    public bool IsInRange(double startBeat, double endBeat)
    {
        return StartBeat < endBeat && GetEndBeat() > startBeat;
    }

    /// <summary>
    /// Creates a deep copy of this note with a new unique identifier.
    /// </summary>
    /// <returns>A new PianoRollNote instance with the same property values but a new Id.</returns>
    public PianoRollNote Clone()
    {
        return new PianoRollNote
        {
            Id = Guid.NewGuid(),
            Note = Note,
            StartBeat = StartBeat,
            Duration = Duration,
            Velocity = Velocity,
            IsSelected = false,
            IsPlaying = false,
            Channel = Channel,
            Color = Color
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
        int octave = GetOctave(midiNote);
        return $"{NoteNames[noteIndex]}{octave}";
    }

    /// <summary>
    /// Gets the octave number for a MIDI note.
    /// </summary>
    /// <param name="midiNote">The MIDI note number (0-127).</param>
    /// <returns>The octave number (MIDI standard: C4 = 60).</returns>
    public static int GetOctave(int midiNote)
    {
        return (midiNote / 12) - 1;
    }

    /// <summary>
    /// Determines if a MIDI note is a black key (sharp/flat).
    /// </summary>
    /// <param name="midiNote">The MIDI note number (0-127).</param>
    /// <returns>True if the note is a black key; otherwise, false.</returns>
    public static bool IsBlackKey(int midiNote)
    {
        int noteIndex = midiNote % 12;
        return BlackKeyPattern[noteIndex];
    }
}

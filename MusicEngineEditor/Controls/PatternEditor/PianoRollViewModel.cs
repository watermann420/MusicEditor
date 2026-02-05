// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Pattern Editor Piano Roll control.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core;

namespace MusicEngineEditor.Controls.PatternEditor;

/// <summary>
/// Represents a single note item in the piano roll.
/// </summary>
public partial class NoteItem : ObservableObject
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private static readonly bool[] BlackKeyPattern = { false, true, false, true, false, false, true, false, true, false, true, false };

    /// <summary>
    /// Unique identifier for the note.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// MIDI pitch (0-127).
    /// </summary>
    [ObservableProperty]
    private int _pitch;

    /// <summary>
    /// Start position in beats.
    /// </summary>
    [ObservableProperty]
    private double _start;

    /// <summary>
    /// Duration in beats.
    /// </summary>
    [ObservableProperty]
    private double _duration = 1.0;

    /// <summary>
    /// Velocity (0-127).
    /// </summary>
    [ObservableProperty]
    private int _velocity = 100;

    /// <summary>
    /// Whether this note is currently selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Whether this note is currently playing.
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// Source pattern identifier for playback mapping.
    /// </summary>
    [ObservableProperty]
    private Guid _sourcePatternId;

    /// <summary>
    /// Source note index within the pattern.
    /// </summary>
    [ObservableProperty]
    private int _sourceNoteIndex = -1;

    /// <summary>
    /// Gets the note name with octave (e.g., "C4", "F#3").
    /// </summary>
    public string NoteName => GetNoteName(Pitch);

    /// <summary>
    /// Gets the end position (Start + Duration).
    /// </summary>
    public double End => Start + Duration;

    partial void OnPitchChanged(int value)
    {
        OnPropertyChanged(nameof(NoteName));
    }

    /// <summary>
    /// Gets the note name for a MIDI pitch value.
    /// </summary>
    public static string GetNoteName(int midiNote)
    {
        if (midiNote < 0 || midiNote > 127)
            return "Invalid";

        int noteIndex = midiNote % 12;
        int octave = (midiNote / 12) - 1;
        return $"{NoteNames[noteIndex]}{octave}";
    }

    /// <summary>
    /// Determines if a MIDI note is a black key.
    /// </summary>
    public static bool IsBlackKey(int midiNote)
    {
        int noteIndex = midiNote % 12;
        return BlackKeyPattern[noteIndex];
    }

    /// <summary>
    /// Creates a clone of this note with a new ID.
    /// </summary>
    public NoteItem Clone()
    {
        return new NoteItem
        {
            Id = Guid.NewGuid(),
            Pitch = Pitch,
            Start = Start,
            Duration = Duration,
            Velocity = Velocity,
            IsSelected = false,
            IsPlaying = false,
            SourcePatternId = SourcePatternId,
            SourceNoteIndex = SourceNoteIndex
        };
    }
}

/// <summary>
/// ViewModel for the Piano Roll Pattern Editor control.
/// </summary>
public partial class PianoRollViewModel : ObservableObject
{
    #region Private Fields

    private static readonly double[] AvailableResolutions = { 0.25, 0.125, 0.0625, 0.03125 };

    #endregion

    #region Observable Properties

    /// <summary>
    /// Collection of all notes in the piano roll.
    /// </summary>
    public ObservableCollection<NoteItem> Notes { get; } = new();

    /// <summary>
    /// Collection of currently selected notes.
    /// </summary>
    public ObservableCollection<NoteItem> SelectedNotes { get; } = new();

    /// <summary>
    /// Grid resolution in beats (0.25 = 1/4, 0.125 = 1/8, 0.0625 = 1/16, 0.03125 = 1/32).
    /// </summary>
    [ObservableProperty]
    private double _gridResolution = 0.25;

    /// <summary>
    /// Whether snap-to-grid is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isSnapEnabled = true;

    /// <summary>
    /// Horizontal zoom level.
    /// </summary>
    [ObservableProperty]
    private double _horizontalZoom = 1.0;

    /// <summary>
    /// Total length of the pattern in beats.
    /// </summary>
    [ObservableProperty]
    private double _totalBeats = 16.0;

    /// <summary>
    /// Lowest visible MIDI note (default 24 = C1).
    /// </summary>
    [ObservableProperty]
    private int _lowestNote = 24; // C1

    /// <summary>
    /// Highest visible MIDI note (default 108 = C8).
    /// </summary>
    [ObservableProperty]
    private int _highestNote = 108; // C8

    /// <summary>
    /// Default velocity for new notes.
    /// </summary>
    [ObservableProperty]
    private int _defaultVelocity = 100;

    /// <summary>
    /// Current playhead position in beats.
    /// </summary>
    [ObservableProperty]
    private double _playheadPosition;

    /// <summary>
    /// Status message for the UI.
    /// </summary>
    [ObservableProperty]
    private string _statusMessage = string.Empty;

    #endregion

    #region Constructor

    public PianoRollViewModel()
    {
        Notes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Notes));
        SelectedNotes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SelectedNotes));
    }

    /// <summary>
    /// Loads notes from a MusicEngine.Core.Pattern into the piano roll.
    /// </summary>
    public void LoadFromPattern(MusicEngine.Core.Pattern pattern)
    {
        Notes.Clear();
        SelectedNotes.Clear();

        TotalBeats = Math.Max(1.0, pattern.LoopLength);

        for (int i = 0; i < pattern.Events.Count; i++)
        {
            var evt = pattern.Events[i];
            var item = new NoteItem
            {
                Pitch = Math.Clamp(evt.Note, 0, 127),
                Start = Math.Max(0, evt.Beat),
                Duration = Math.Max(GridResolution, evt.Duration),
                Velocity = Math.Clamp(evt.Velocity, 0, 127),
                SourcePatternId = pattern.Id,
                SourceNoteIndex = i
            };

            Notes.Add(item);
        }
    }

    #endregion

    #region Note Management

    /// <summary>
    /// Adds a new note to the piano roll.
    /// </summary>
    /// <param name="pitch">MIDI pitch (0-127).</param>
    /// <param name="start">Start position in beats.</param>
    /// <param name="duration">Duration in beats.</param>
    /// <param name="velocity">Velocity (0-127).</param>
    /// <returns>The created note.</returns>
    public NoteItem AddNote(int pitch, double start, double duration, int velocity)
    {
        var note = new NoteItem
        {
            Pitch = Math.Clamp(pitch, 0, 127),
            Start = IsSnapEnabled ? SnapToGrid(start) : Math.Max(0, start),
            Duration = Math.Max(GridResolution, duration),
            Velocity = Math.Clamp(velocity, 0, 127)
        };

        Notes.Add(note);
        StatusMessage = $"Added note {note.NoteName} at beat {note.Start:F2}";
        return note;
    }

    /// <summary>
    /// Deletes a note from the piano roll.
    /// </summary>
    /// <param name="note">The note to delete.</param>
    public void DeleteNote(NoteItem note)
    {
        if (note == null) return;

        if (SelectedNotes.Contains(note))
        {
            SelectedNotes.Remove(note);
        }

        Notes.Remove(note);
        StatusMessage = $"Deleted note {note.NoteName}";
    }

    /// <summary>
    /// Gets a note at the specified position.
    /// </summary>
    /// <param name="pitch">The MIDI pitch.</param>
    /// <param name="beat">The beat position.</param>
    /// <returns>The note at the position, or null if none found.</returns>
    public NoteItem? GetNoteAt(int pitch, double beat)
    {
        return Notes.FirstOrDefault(n =>
            n.Pitch == pitch &&
            beat >= n.Start &&
            beat < n.End);
    }

    #endregion

    #region Selection Commands

    /// <summary>
    /// Selects all notes.
    /// </summary>
    [RelayCommand]
    private void SelectAll()
    {
        SelectedNotes.Clear();
        foreach (var note in Notes)
        {
            note.IsSelected = true;
            SelectedNotes.Add(note);
        }
        StatusMessage = $"Selected {Notes.Count} note(s)";
    }

    /// <summary>
    /// Deselects all notes.
    /// </summary>
    [RelayCommand]
    private void DeselectAll()
    {
        foreach (var note in SelectedNotes)
        {
            note.IsSelected = false;
        }
        SelectedNotes.Clear();
        StatusMessage = "Deselected all notes";
    }

    /// <summary>
    /// Deletes all selected notes.
    /// </summary>
    [RelayCommand]
    private void DeleteSelected()
    {
        var notesToRemove = SelectedNotes.ToList();
        foreach (var note in notesToRemove)
        {
            Notes.Remove(note);
        }
        SelectedNotes.Clear();
        StatusMessage = $"Deleted {notesToRemove.Count} note(s)";
    }

    #endregion

    #region Quantize Commands

    /// <summary>
    /// Quantizes all selected notes to the current grid resolution.
    /// </summary>
    [RelayCommand]
    private void QuantizeSelected()
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected to quantize";
            return;
        }

        foreach (var note in SelectedNotes)
        {
            note.Start = SnapToGrid(note.Start);
            note.Duration = Math.Max(GridResolution, SnapToGrid(note.Duration));
        }

        StatusMessage = $"Quantized {SelectedNotes.Count} note(s) to 1/{(int)(4 / GridResolution)}";
    }

    /// <summary>
    /// Quantizes start positions only (preserves duration).
    /// </summary>
    [RelayCommand]
    private void QuantizeStartOnly()
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected to quantize";
            return;
        }

        foreach (var note in SelectedNotes)
        {
            note.Start = SnapToGrid(note.Start);
        }

        StatusMessage = $"Quantized start positions of {SelectedNotes.Count} note(s)";
    }

    #endregion

    #region Grid Commands

    /// <summary>
    /// Sets the grid resolution.
    /// </summary>
    /// <param name="resolution">The resolution value in beats.</param>
    [RelayCommand]
    private void SetGridResolution(double resolution)
    {
        if (resolution > 0 && AvailableResolutions.Contains(resolution))
        {
            GridResolution = resolution;
            StatusMessage = $"Grid set to 1/{(int)(4 / resolution)}";
        }
    }

    /// <summary>
    /// Toggles snap-to-grid.
    /// </summary>
    [RelayCommand]
    private void ToggleSnap()
    {
        IsSnapEnabled = !IsSnapEnabled;
        StatusMessage = IsSnapEnabled ? "Snap enabled" : "Snap disabled";
    }

    #endregion

    #region Zoom Commands

    /// <summary>
    /// Zooms in horizontally.
    /// </summary>
    [RelayCommand]
    private void ZoomIn()
    {
        HorizontalZoom = Math.Min(4.0, HorizontalZoom + 0.25);
    }

    /// <summary>
    /// Zooms out horizontally.
    /// </summary>
    [RelayCommand]
    private void ZoomOut()
    {
        HorizontalZoom = Math.Max(0.25, HorizontalZoom - 0.25);
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Snaps a beat position to the current grid.
    /// </summary>
    /// <param name="beat">The beat position to snap.</param>
    /// <returns>The snapped beat position.</returns>
    public double SnapToGrid(double beat)
    {
        if (GridResolution <= 0) return beat;
        return Math.Round(beat / GridResolution) * GridResolution;
    }

    /// <summary>
    /// Selects a note.
    /// </summary>
    /// <param name="note">The note to select.</param>
    /// <param name="addToSelection">Whether to add to existing selection.</param>
    public void SelectNote(NoteItem note, bool addToSelection = false)
    {
        if (!addToSelection)
        {
            DeselectAll();
        }

        if (!SelectedNotes.Contains(note))
        {
            note.IsSelected = true;
            SelectedNotes.Add(note);
        }
    }

    /// <summary>
    /// Deselects a note.
    /// </summary>
    /// <param name="note">The note to deselect.</param>
    public void DeselectNote(NoteItem note)
    {
        note.IsSelected = false;
        SelectedNotes.Remove(note);
    }

    #endregion
}

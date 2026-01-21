// MusicEngineEditor - Piano Roll ViewModel
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Defines the available tools for the Piano Roll editor.
/// </summary>
public enum PianoRollTool
{
    /// <summary>
    /// Selection tool for selecting and moving notes.
    /// </summary>
    Select,

    /// <summary>
    /// Draw tool for creating new notes.
    /// </summary>
    Draw,

    /// <summary>
    /// Erase tool for deleting notes.
    /// </summary>
    Erase,

    /// <summary>
    /// Slice tool for splitting notes at a position.
    /// </summary>
    Slice
}

/// <summary>
/// ViewModel for the Piano Roll editor, managing notes, selection, tools, and playback state.
/// </summary>
public partial class PianoRollViewModel : ViewModelBase
{
    #region Constants

    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;
    private const double ZoomStep = 0.25;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the collection of all notes in the piano roll.
    /// </summary>
    public ObservableCollection<PianoRollNote> Notes { get; } = new();

    /// <summary>
    /// Gets the collection of currently selected notes.
    /// </summary>
    public ObservableCollection<PianoRollNote> SelectedNotes { get; } = new();

    #endregion

    #region Tool Properties

    /// <summary>
    /// Gets or sets the currently active tool.
    /// </summary>
    [ObservableProperty]
    private PianoRollTool _currentTool = PianoRollTool.Select;

    /// <summary>
    /// Gets or sets the grid snap value in beats (0.25 = 16th, 0.5 = 8th, 1.0 = quarter).
    /// </summary>
    [ObservableProperty]
    private double _gridSnapValue = 0.25;

    #endregion

    #region Zoom and Scroll Properties

    /// <summary>
    /// Gets or sets the horizontal zoom level.
    /// </summary>
    [ObservableProperty]
    private double _zoomX = 1.0;

    /// <summary>
    /// Gets or sets the vertical zoom level.
    /// </summary>
    [ObservableProperty]
    private double _zoomY = 1.0;

    /// <summary>
    /// Gets or sets the horizontal scroll position in beats.
    /// </summary>
    [ObservableProperty]
    private double _scrollX;

    /// <summary>
    /// Gets or sets the vertical scroll position (note range).
    /// </summary>
    [ObservableProperty]
    private double _scrollY;

    #endregion

    #region Playback Properties

    /// <summary>
    /// Gets or sets the current playhead position in beats.
    /// </summary>
    [ObservableProperty]
    private double _playheadPosition;

    /// <summary>
    /// Gets or sets whether playback is currently active.
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// Gets or sets the loop start position in beats.
    /// </summary>
    [ObservableProperty]
    private double _loopStart;

    /// <summary>
    /// Gets or sets the loop end position in beats.
    /// </summary>
    [ObservableProperty]
    private double _loopEnd = 16.0;

    /// <summary>
    /// Gets or sets whether looping is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _loopEnabled;

    #endregion

    #region Pattern Properties

    /// <summary>
    /// Gets or sets the total length of the pattern in beats.
    /// </summary>
    [ObservableProperty]
    private double _totalBeats = 16.0;

    /// <summary>
    /// Gets or sets the lowest visible MIDI note (default 36 = C2).
    /// </summary>
    [ObservableProperty]
    private int _lowestNote = 36;

    /// <summary>
    /// Gets or sets the highest visible MIDI note (default 96 = C7).
    /// </summary>
    [ObservableProperty]
    private int _highestNote = 96;

    #endregion

    #region Default Note Properties

    /// <summary>
    /// Gets or sets the default velocity for new notes (0-127).
    /// </summary>
    [ObservableProperty]
    private int _defaultVelocity = 100;

    /// <summary>
    /// Gets or sets the default duration for new notes in beats.
    /// </summary>
    [ObservableProperty]
    private double _defaultDuration = 1.0;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new PianoRollViewModel instance.
    /// </summary>
    public PianoRollViewModel()
    {
        Notes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Notes));
        SelectedNotes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SelectedNotes));
    }

    #endregion

    #region Selection Commands

    /// <summary>
    /// Selects all notes in the piano roll.
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
    }

    /// <summary>
    /// Duplicates all selected notes.
    /// </summary>
    [RelayCommand]
    private void DuplicateSelected()
    {
        if (SelectedNotes.Count == 0)
            return;

        var duplicates = new List<PianoRollNote>();
        double offset = GridSnapValue; // Offset duplicates by one grid unit

        foreach (var note in SelectedNotes)
        {
            var duplicate = note.Clone();
            duplicate.StartBeat += offset;
            duplicates.Add(duplicate);
        }

        // Deselect original notes
        DeselectAll();

        // Add and select duplicates
        foreach (var duplicate in duplicates)
        {
            Notes.Add(duplicate);
            duplicate.IsSelected = true;
            SelectedNotes.Add(duplicate);
        }
    }

    /// <summary>
    /// Quantizes selected notes to the current grid.
    /// </summary>
    [RelayCommand]
    private void QuantizeSelected()
    {
        foreach (var note in SelectedNotes)
        {
            note.StartBeat = SnapToBeat(note.StartBeat);
            note.Duration = Math.Max(GridSnapValue, SnapToBeat(note.Duration));
        }
    }

    #endregion

    #region Transpose Command

    /// <summary>
    /// Transposes selected notes by the specified number of semitones.
    /// </summary>
    /// <param name="semitones">Number of semitones to transpose (positive = up, negative = down).</param>
    [RelayCommand]
    private void Transpose(int semitones)
    {
        foreach (var note in SelectedNotes)
        {
            int newNote = note.Note + semitones;
            // Clamp to valid MIDI range
            note.Note = Math.Clamp(newNote, 0, 127);
        }
    }

    #endregion

    #region Tool Commands

    /// <summary>
    /// Sets the current editing tool.
    /// </summary>
    /// <param name="tool">The tool to activate.</param>
    [RelayCommand]
    private void SetTool(PianoRollTool tool)
    {
        CurrentTool = tool;
    }

    /// <summary>
    /// Sets the grid snap value.
    /// </summary>
    /// <param name="value">The snap value in beats.</param>
    [RelayCommand]
    private void SetGridSnap(double value)
    {
        if (value > 0)
        {
            GridSnapValue = value;
        }
    }

    #endregion

    #region Zoom Commands

    /// <summary>
    /// Increases horizontal zoom.
    /// </summary>
    [RelayCommand]
    private void ZoomInX()
    {
        ZoomX = Math.Min(MaxZoom, ZoomX + ZoomStep);
    }

    /// <summary>
    /// Decreases horizontal zoom.
    /// </summary>
    [RelayCommand]
    private void ZoomOutX()
    {
        ZoomX = Math.Max(MinZoom, ZoomX - ZoomStep);
    }

    /// <summary>
    /// Increases vertical zoom.
    /// </summary>
    [RelayCommand]
    private void ZoomInY()
    {
        ZoomY = Math.Min(MaxZoom, ZoomY + ZoomStep);
    }

    /// <summary>
    /// Decreases vertical zoom.
    /// </summary>
    [RelayCommand]
    private void ZoomOutY()
    {
        ZoomY = Math.Max(MinZoom, ZoomY - ZoomStep);
    }

    #endregion

    #region Undo/Redo Commands (Placeholder)

    /// <summary>
    /// Undoes the last action (placeholder implementation).
    /// </summary>
    [RelayCommand]
    private void Undo()
    {
        // TODO: Implement undo functionality with command pattern or memento
        StatusMessage = "Undo not yet implemented";
    }

    /// <summary>
    /// Redoes the last undone action (placeholder implementation).
    /// </summary>
    [RelayCommand]
    private void Redo()
    {
        // TODO: Implement redo functionality with command pattern or memento
        StatusMessage = "Redo not yet implemented";
    }

    #endregion

    #region Note Management Methods

    /// <summary>
    /// Adds a new note to the piano roll.
    /// </summary>
    /// <param name="note">MIDI note number (0-127).</param>
    /// <param name="startBeat">Start position in beats.</param>
    /// <param name="duration">Duration in beats.</param>
    /// <param name="velocity">Velocity (0-127).</param>
    /// <returns>The created note.</returns>
    public PianoRollNote AddNote(int note, double startBeat, double duration, int velocity)
    {
        var pianoNote = new PianoRollNote
        {
            Note = Math.Clamp(note, 0, 127),
            StartBeat = Math.Max(0, startBeat),
            Duration = Math.Max(GridSnapValue, duration),
            Velocity = Math.Clamp(velocity, 0, 127)
        };

        Notes.Add(pianoNote);
        return pianoNote;
    }

    /// <summary>
    /// Removes a note from the piano roll.
    /// </summary>
    /// <param name="note">The note to remove.</param>
    /// <returns>True if the note was removed; otherwise, false.</returns>
    public bool RemoveNote(PianoRollNote note)
    {
        if (note.IsSelected)
        {
            SelectedNotes.Remove(note);
        }
        return Notes.Remove(note);
    }

    /// <summary>
    /// Moves notes by the specified beat and note deltas.
    /// </summary>
    /// <param name="notes">The notes to move.</param>
    /// <param name="beatDelta">Horizontal movement in beats.</param>
    /// <param name="noteDelta">Vertical movement in semitones.</param>
    public void MoveNotes(IEnumerable<PianoRollNote> notes, double beatDelta, int noteDelta)
    {
        foreach (var note in notes)
        {
            double newStartBeat = note.StartBeat + beatDelta;
            int newNoteValue = note.Note + noteDelta;

            // Clamp values to valid ranges
            note.StartBeat = Math.Max(0, newStartBeat);
            note.Note = Math.Clamp(newNoteValue, 0, 127);
        }
    }

    /// <summary>
    /// Resizes notes by adjusting their duration.
    /// </summary>
    /// <param name="notes">The notes to resize.</param>
    /// <param name="durationDelta">Change in duration (positive = longer, negative = shorter).</param>
    public void ResizeNotes(IEnumerable<PianoRollNote> notes, double durationDelta)
    {
        foreach (var note in notes)
        {
            double newDuration = note.Duration + durationDelta;
            // Ensure minimum duration of one grid unit
            note.Duration = Math.Max(GridSnapValue, newDuration);
        }
    }

    /// <summary>
    /// Snaps a beat position to the current grid.
    /// </summary>
    /// <param name="beat">The beat position to snap.</param>
    /// <returns>The snapped beat position.</returns>
    public double SnapToBeat(double beat)
    {
        if (GridSnapValue <= 0)
            return beat;

        return Math.Round(beat / GridSnapValue) * GridSnapValue;
    }

    /// <summary>
    /// Finds a note at the specified position.
    /// </summary>
    /// <param name="beat">The beat position to check.</param>
    /// <param name="note">The MIDI note number to check.</param>
    /// <returns>The note at the position, or null if none found.</returns>
    public PianoRollNote? GetNoteAtPosition(double beat, int note)
    {
        return Notes.FirstOrDefault(n =>
            n.Note == note &&
            beat >= n.StartBeat &&
            beat < n.GetEndBeat());
    }

    #endregion

    #region Property Change Handlers

    partial void OnZoomXChanged(double value)
    {
        ZoomX = Math.Clamp(value, MinZoom, MaxZoom);
    }

    partial void OnZoomYChanged(double value)
    {
        ZoomY = Math.Clamp(value, MinZoom, MaxZoom);
    }

    partial void OnLoopStartChanged(double value)
    {
        // Ensure loop start doesn't exceed loop end
        if (value >= LoopEnd)
        {
            LoopStart = Math.Max(0, LoopEnd - GridSnapValue);
        }
    }

    partial void OnLoopEndChanged(double value)
    {
        // Ensure loop end doesn't go below loop start
        if (value <= LoopStart)
        {
            LoopEnd = LoopStart + GridSnapValue;
        }
    }

    partial void OnTotalBeatsChanged(double value)
    {
        // Ensure total beats is at least one grid unit
        if (value < GridSnapValue)
        {
            TotalBeats = GridSnapValue;
        }

        // Adjust loop end if it exceeds total beats
        if (LoopEnd > TotalBeats)
        {
            LoopEnd = TotalBeats;
        }
    }

    #endregion
}

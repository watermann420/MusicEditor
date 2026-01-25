// MusicEngineEditor - Piano Roll ViewModel
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Commands;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

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
/// Integrates with PlaybackService for synchronized playback and note preview.
/// </summary>
public partial class PianoRollViewModel : ViewModelBase, IDisposable
{
    #region Constants

    private const double MinZoom = 0.1;
    private const double MaxZoom = 10.0;
    private const double ZoomStep = 0.25;

    #endregion

    #region Private Fields

    private readonly PlaybackService _playbackService;
    private readonly AudioEngineService _audioEngineService;
    private EventBus.SubscriptionToken? _beatSubscription;
    private EventBus.SubscriptionToken? _playbackStartedSubscription;
    private EventBus.SubscriptionToken? _playbackStoppedSubscription;
    private bool _disposed;

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

    /// <summary>
    /// Gets the collection of all MIDI CC events across all controllers.
    /// </summary>
    public ObservableCollection<MidiCCEvent> AllCCEvents { get; } = new();

    /// <summary>
    /// Gets the collection of CC lane view models.
    /// </summary>
    public ObservableCollection<MidiCCLaneViewModel> CCLanes { get; } = new();

    #endregion

    #region CC Lane Properties

    /// <summary>
    /// Gets or sets the currently selected CC lane.
    /// </summary>
    [ObservableProperty]
    private MidiCCLaneViewModel? _selectedCCLane;

    /// <summary>
    /// Gets or sets whether the CC lanes section is expanded.
    /// </summary>
    [ObservableProperty]
    private bool _ccLanesExpanded = true;

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

    #region Scale Highlighting Properties

    /// <summary>
    /// Gets or sets whether scale highlighting is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _scaleHighlightingEnabled;

    /// <summary>
    /// Gets or sets the root note for scale highlighting (0-11).
    /// </summary>
    [ObservableProperty]
    private int _scaleRoot;

    /// <summary>
    /// Gets or sets the scale type name.
    /// </summary>
    [ObservableProperty]
    private string _scaleType = "Major";

    #endregion

    #region Clipboard and Preview Properties

    /// <summary>
    /// Internal clipboard for copied notes.
    /// </summary>
    private List<PianoRollNote> _clipboard = [];

    /// <summary>
    /// Gets whether the clipboard has content.
    /// </summary>
    public bool HasClipboardContent => _clipboard.Count > 0;

    /// <summary>
    /// Gets or sets whether note preview (sound playback while drawing) is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _notePreviewEnabled = true;

    /// <summary>
    /// Event raised when a note should be previewed (played).
    /// </summary>
    public event EventHandler<NotePreviewEventArgs>? NotePreviewRequested;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new PianoRollViewModel instance.
    /// </summary>
    public PianoRollViewModel()
    {
        _playbackService = PlaybackService.Instance;
        _audioEngineService = AudioEngineService.Instance;

        Notes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Notes));
        SelectedNotes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SelectedNotes));

        // Subscribe to playback events
        SubscribeToPlaybackEvents();
    }

    #endregion

    #region Playback Integration

    /// <summary>
    /// Subscribes to playback service events for synchronized playback.
    /// </summary>
    private void SubscribeToPlaybackEvents()
    {
        // Subscribe via EventBus for thread-safe UI updates
        var eventBus = EventBus.Instance;

        _beatSubscription = eventBus.SubscribeBeatChanged(OnBeatChanged);
        _playbackStartedSubscription = eventBus.SubscribePlaybackStarted(OnPlaybackStarted);
        _playbackStoppedSubscription = eventBus.SubscribePlaybackStopped(OnPlaybackStopped);
    }

    private void OnBeatChanged(EventBus.BeatChangedEventArgs args)
    {
        // Update playhead position
        PlayheadPosition = args.CurrentBeat;

        // Handle looping within the piano roll
        if (LoopEnabled && PlayheadPosition >= LoopEnd)
        {
            // Let the PlaybackService handle global looping
            // This is just for visual feedback
        }

        // Trigger notes at current position during playback
        if (IsPlaying)
        {
            TriggerNotesAtPosition(args.CurrentBeat);
        }
    }

    private void OnPlaybackStarted(EventBus.PlaybackStartedEventArgs args)
    {
        IsPlaying = true;
    }

    private void OnPlaybackStopped(EventBus.PlaybackStoppedEventArgs args)
    {
        IsPlaying = false;
        AllNotesOff();
    }

    /// <summary>
    /// Triggers notes that should sound at the given beat position.
    /// </summary>
    /// <param name="beat">The current beat position.</param>
    private void TriggerNotesAtPosition(double beat)
    {
        // Find notes that start at this beat (within a small tolerance)
        const double tolerance = 0.05; // About 50ms at 120 BPM

        foreach (var note in Notes)
        {
            if (Math.Abs(note.StartBeat - beat) < tolerance)
            {
                // Trigger note on
                PlayNoteDuringPlayback(note);
            }
            else if (Math.Abs(note.GetEndBeat() - beat) < tolerance)
            {
                // Trigger note off
                StopNoteDuringPlayback(note);
            }
        }
    }

    /// <summary>
    /// Plays a note during playback through the audio engine.
    /// </summary>
    /// <param name="note">The note to play.</param>
    private void PlayNoteDuringPlayback(PianoRollNote note)
    {
        try
        {
            _audioEngineService.PlayNotePreview(note.Note, note.Velocity);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error playing note: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops a note during playback.
    /// </summary>
    /// <param name="note">The note to stop.</param>
    private void StopNoteDuringPlayback(PianoRollNote note)
    {
        try
        {
            _audioEngineService.StopNotePreview(note.Note);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping note: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops all currently playing notes.
    /// </summary>
    private void AllNotesOff()
    {
        try
        {
            _audioEngineService.AllNotesOff();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error stopping all notes: {ex.Message}");
        }
    }

    #endregion

    #region Playback Commands

    /// <summary>
    /// Starts or resumes playback.
    /// </summary>
    [RelayCommand]
    private void Play()
    {
        _playbackService.Play();
    }

    /// <summary>
    /// Pauses playback.
    /// </summary>
    [RelayCommand]
    private void PausePlayback()
    {
        _playbackService.Pause();
    }

    /// <summary>
    /// Stops playback and resets position.
    /// </summary>
    [RelayCommand]
    private void StopPlayback()
    {
        _playbackService.Stop();
        PlayheadPosition = 0;
    }

    /// <summary>
    /// Toggles play/pause.
    /// </summary>
    [RelayCommand]
    private void TogglePlayPause()
    {
        _playbackService.TogglePlayPause();
    }

    /// <summary>
    /// Jumps playhead to the specified beat position.
    /// </summary>
    /// <param name="beat">The beat position to jump to.</param>
    [RelayCommand]
    private void JumpToPosition(double beat)
    {
        _playbackService.SetPosition(beat);
        PlayheadPosition = beat;
    }

    /// <summary>
    /// Toggles loop mode.
    /// </summary>
    [RelayCommand]
    private void ToggleLoopMode()
    {
        LoopEnabled = !LoopEnabled;
        _playbackService.LoopEnabled = LoopEnabled;
    }

    /// <summary>
    /// Sets the loop region from the current pattern bounds.
    /// </summary>
    [RelayCommand]
    private void SetLoopToPattern()
    {
        _playbackService.SetLoopRegion(0, TotalBeats);
        LoopStart = 0;
        LoopEnd = TotalBeats;
    }

    /// <summary>
    /// Sets the loop region from selected notes.
    /// </summary>
    [RelayCommand]
    private void SetLoopToSelection()
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected";
            return;
        }

        var minStart = SelectedNotes.Min(n => n.StartBeat);
        var maxEnd = SelectedNotes.Max(n => n.GetEndBeat());

        LoopStart = minStart;
        LoopEnd = maxEnd;
        _playbackService.SetLoopRegion(minStart, maxEnd);
        StatusMessage = $"Loop set: {minStart:F1} - {maxEnd:F1}";
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

    #region Undo/Redo Integration

    /// <summary>
    /// Gets the editor undo service.
    /// </summary>
    private EditorUndoService UndoService => EditorUndoService.Instance;

    /// <summary>
    /// Adds a note with undo support.
    /// </summary>
    /// <param name="note">MIDI note number (0-127).</param>
    /// <param name="startBeat">Start position in beats.</param>
    /// <param name="duration">Duration in beats.</param>
    /// <param name="velocity">Velocity (0-127).</param>
    /// <returns>The created note.</returns>
    public PianoRollNote AddNoteWithUndo(int note, double startBeat, double duration, int velocity)
    {
        var pianoNote = new PianoRollNote
        {
            Note = Math.Clamp(note, 0, 127),
            StartBeat = Math.Max(0, startBeat),
            Duration = Math.Max(GridSnapValue, duration),
            Velocity = Math.Clamp(velocity, 0, 127)
        };

        var command = new NoteAddCommand(Notes, pianoNote);
        UndoService.Execute(command);
        return pianoNote;
    }

    /// <summary>
    /// Removes a note with undo support.
    /// </summary>
    /// <param name="note">The note to remove.</param>
    public void RemoveNoteWithUndo(PianoRollNote note)
    {
        if (note.IsSelected)
        {
            SelectedNotes.Remove(note);
        }
        var command = new NoteDeleteCommand(Notes, note);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Deletes selected notes with undo support.
    /// </summary>
    [RelayCommand]
    private void DeleteSelectedWithUndo()
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected to delete";
            return;
        }

        var notesToRemove = SelectedNotes.ToList();
        var command = new NoteDeleteCommand(Notes, notesToRemove);
        UndoService.Execute(command);
        SelectedNotes.Clear();
        StatusMessage = $"Deleted {notesToRemove.Count} note(s)";
    }

    /// <summary>
    /// Moves notes with undo support.
    /// </summary>
    /// <param name="notes">The notes to move.</param>
    /// <param name="beatDelta">Horizontal movement in beats.</param>
    /// <param name="noteDelta">Vertical movement in semitones.</param>
    public void MoveNotesWithUndo(IEnumerable<PianoRollNote> notes, double beatDelta, int noteDelta)
    {
        var notesList = notes.ToList();
        if (notesList.Count == 0) return;

        var newPositions = notesList.Select(n => (
            Math.Max(0, n.StartBeat + beatDelta),
            Math.Clamp(n.Note + noteDelta, 0, 127)
        )).ToList();

        var command = new NoteMoveCommand(notesList, newPositions);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Resizes notes with undo support.
    /// </summary>
    /// <param name="notes">The notes to resize.</param>
    /// <param name="newDurations">The new durations.</param>
    public void ResizeNotesWithUndo(IEnumerable<PianoRollNote> notes, IEnumerable<double> newDurations)
    {
        var notesList = notes.ToList();
        var durationsList = newDurations.Select(d => Math.Max(GridSnapValue, d)).ToList();

        if (notesList.Count == 0 || notesList.Count != durationsList.Count) return;

        var command = new NoteResizeCommand(notesList, durationsList);
        UndoService.Execute(command);
    }

    /// <summary>
    /// Sets velocity of selected notes with undo support.
    /// </summary>
    /// <param name="velocity">The velocity value (0-127).</param>
    [RelayCommand]
    private void SetVelocityWithUndo(int velocity)
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected";
            return;
        }

        velocity = Math.Clamp(velocity, 0, 127);
        var command = new NoteVelocityCommand(SelectedNotes, velocity);
        UndoService.Execute(command);
        StatusMessage = $"Set velocity to {velocity} for {SelectedNotes.Count} note(s)";
    }

    /// <summary>
    /// Transposes selected notes with undo support.
    /// </summary>
    /// <param name="semitones">Number of semitones to transpose.</param>
    [RelayCommand]
    private void TransposeWithUndo(int semitones)
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected";
            return;
        }

        var notesList = SelectedNotes.ToList();
        var newPositions = notesList.Select(n => (
            n.StartBeat,
            Math.Clamp(n.Note + semitones, 0, 127)
        )).ToList();

        var command = new NoteMoveCommand(notesList, newPositions);
        UndoService.Execute(command);
        StatusMessage = $"Transposed {SelectedNotes.Count} note(s) by {semitones} semitones";
    }

    /// <summary>
    /// Pastes notes with undo support.
    /// </summary>
    [RelayCommand]
    private void PasteWithUndo()
    {
        if (_clipboard.Count == 0)
        {
            StatusMessage = "Clipboard is empty";
            return;
        }

        DeselectAll();
        double pastePosition = PlayheadPosition;

        using var batch = UndoService.BeginBatch($"Paste {_clipboard.Count} Note(s)");

        foreach (var clipboardNote in _clipboard)
        {
            var newNote = clipboardNote.Clone();
            newNote.StartBeat = pastePosition + clipboardNote.StartBeat;
            newNote.IsSelected = true;

            var command = new NoteAddCommand(Notes, newNote);
            batch.Execute(command);
            SelectedNotes.Add(newNote);
        }

        StatusMessage = $"Pasted {_clipboard.Count} note(s) at beat {pastePosition:F2}";
    }

    /// <summary>
    /// Cuts selected notes with undo support.
    /// </summary>
    [RelayCommand]
    private void CutWithUndo()
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected to cut";
            return;
        }

        Copy();
        var count = SelectedNotes.Count;
        DeleteSelectedWithUndo();
        StatusMessage = $"Cut {count} note(s)";
    }

    /// <summary>
    /// Duplicates selected notes with undo support.
    /// </summary>
    [RelayCommand]
    private void DuplicateSelectedWithUndo()
    {
        if (SelectedNotes.Count == 0)
            return;

        var duplicates = new List<PianoRollNote>();
        double offset = GridSnapValue;

        using var batch = UndoService.BeginBatch($"Duplicate {SelectedNotes.Count} Note(s)");

        foreach (var note in SelectedNotes)
        {
            var duplicate = note.Clone();
            duplicate.StartBeat += offset;
            duplicate.IsSelected = false;

            var command = new NoteAddCommand(Notes, duplicate);
            batch.Execute(command);
            duplicates.Add(duplicate);
        }

        // Deselect original notes and select duplicates
        DeselectAll();
        foreach (var duplicate in duplicates)
        {
            duplicate.IsSelected = true;
            SelectedNotes.Add(duplicate);
        }

        StatusMessage = $"Duplicated {duplicates.Count} note(s)";
    }

    #endregion

    #region Copy/Paste Commands

    /// <summary>
    /// Copies the selected notes to the internal clipboard.
    /// </summary>
    [RelayCommand]
    private void Copy()
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected to copy";
            return;
        }

        _clipboard.Clear();

        // Find the earliest start beat among selected notes for relative positioning
        double minStartBeat = SelectedNotes.Min(n => n.StartBeat);

        foreach (var note in SelectedNotes)
        {
            var copiedNote = note.Clone();
            // Store relative position from the earliest note
            copiedNote.StartBeat = note.StartBeat - minStartBeat;
            _clipboard.Add(copiedNote);
        }

        OnPropertyChanged(nameof(HasClipboardContent));
        StatusMessage = $"Copied {_clipboard.Count} note(s)";
    }

    /// <summary>
    /// Pastes notes from the clipboard at the current playhead position.
    /// </summary>
    [RelayCommand]
    private void Paste()
    {
        if (_clipboard.Count == 0)
        {
            StatusMessage = "Clipboard is empty";
            return;
        }

        // Deselect current selection
        DeselectAll();

        // Paste at playhead position
        double pastePosition = PlayheadPosition;

        var pastedNotes = new List<PianoRollNote>();
        foreach (var clipboardNote in _clipboard)
        {
            var newNote = clipboardNote.Clone();
            newNote.StartBeat = pastePosition + clipboardNote.StartBeat;
            newNote.IsSelected = true;
            Notes.Add(newNote);
            SelectedNotes.Add(newNote);
            pastedNotes.Add(newNote);
        }

        StatusMessage = $"Pasted {pastedNotes.Count} note(s) at beat {pastePosition:F2}";
    }

    /// <summary>
    /// Cuts the selected notes (copy + delete).
    /// </summary>
    [RelayCommand]
    private void Cut()
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected to cut";
            return;
        }

        Copy();
        var count = SelectedNotes.Count;
        DeleteSelected();
        StatusMessage = $"Cut {count} note(s)";
    }

    /// <summary>
    /// Checks if copy operation can be executed.
    /// </summary>
    private bool CanCopy() => SelectedNotes.Count > 0;

    /// <summary>
    /// Checks if paste operation can be executed.
    /// </summary>
    private bool CanPaste() => _clipboard.Count > 0;

    /// <summary>
    /// Checks if cut operation can be executed.
    /// </summary>
    private bool CanCut() => SelectedNotes.Count > 0;

    #endregion

    #region Quantize Commands

    /// <summary>
    /// Quantizes selected notes to the specified grid value.
    /// </summary>
    /// <param name="gridValue">The grid value to quantize to (e.g., 0.25 for 1/16, 0.5 for 1/8).</param>
    [RelayCommand]
    private void QuantizeToGrid(double gridValue)
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected to quantize";
            return;
        }

        if (gridValue <= 0)
        {
            gridValue = GridSnapValue;
        }

        foreach (var note in SelectedNotes)
        {
            note.StartBeat = Math.Round(note.StartBeat / gridValue) * gridValue;
            note.Duration = Math.Max(gridValue, Math.Round(note.Duration / gridValue) * gridValue);
        }

        StatusMessage = $"Quantized {SelectedNotes.Count} note(s) to 1/{(int)(4 / gridValue)}";
    }

    /// <summary>
    /// Quantizes note start positions only (preserves duration).
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
            note.StartBeat = SnapToBeat(note.StartBeat);
        }

        StatusMessage = $"Quantized start positions of {SelectedNotes.Count} note(s)";
    }

    /// <summary>
    /// Quantizes note end positions only (adjusts duration).
    /// </summary>
    [RelayCommand]
    private void QuantizeEndOnly()
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected to quantize";
            return;
        }

        foreach (var note in SelectedNotes)
        {
            double snappedEnd = SnapToBeat(note.GetEndBeat());
            double newDuration = snappedEnd - note.StartBeat;
            note.Duration = Math.Max(GridSnapValue, newDuration);
        }

        StatusMessage = $"Quantized end positions of {SelectedNotes.Count} note(s)";
    }

    #endregion

    #region Velocity Commands

    /// <summary>
    /// Sets the velocity of all selected notes.
    /// </summary>
    /// <param name="velocity">The velocity value (0-127).</param>
    [RelayCommand]
    private void SetVelocity(int velocity)
    {
        velocity = Math.Clamp(velocity, 0, 127);
        foreach (var note in SelectedNotes)
        {
            note.Velocity = velocity;
        }
        StatusMessage = $"Set velocity to {velocity} for {SelectedNotes.Count} note(s)";
    }

    /// <summary>
    /// Adjusts the velocity of all selected notes by a delta.
    /// </summary>
    /// <param name="delta">The velocity change (positive or negative).</param>
    [RelayCommand]
    private void AdjustVelocity(int delta)
    {
        foreach (var note in SelectedNotes)
        {
            note.Velocity = Math.Clamp(note.Velocity + delta, 0, 127);
        }
        StatusMessage = $"Adjusted velocity by {delta:+#;-#;0} for {SelectedNotes.Count} note(s)";
    }

    /// <summary>
    /// Creates a velocity ramp across selected notes (humanization).
    /// </summary>
    /// <param name="startVelocity">Starting velocity.</param>
    /// <param name="endVelocity">Ending velocity.</param>
    [RelayCommand]
    private void CreateVelocityRamp((int start, int end) velocities)
    {
        if (SelectedNotes.Count < 2)
        {
            StatusMessage = "Need at least 2 notes for velocity ramp";
            return;
        }

        var sortedNotes = SelectedNotes.OrderBy(n => n.StartBeat).ToList();
        int count = sortedNotes.Count;

        for (int i = 0; i < count; i++)
        {
            double t = (double)i / (count - 1);
            int velocity = (int)Math.Round(velocities.start + (velocities.end - velocities.start) * t);
            sortedNotes[i].Velocity = Math.Clamp(velocity, 0, 127);
        }

        StatusMessage = $"Created velocity ramp {velocities.start} to {velocities.end}";
    }

    #endregion

    #region CC Lane Commands

    /// <summary>
    /// Adds a new CC lane with the specified controller.
    /// </summary>
    /// <param name="controller">The CC controller number (0-127). If null, uses a default controller.</param>
    [RelayCommand]
    private void AddCCLane(int? controller = null)
    {
        // Find a controller not already used, or use the specified one
        int ccNumber = controller ?? GetNextAvailableCCController();

        var newLane = new MidiCCLaneViewModel(AllCCEvents)
        {
            SelectedController = ccNumber
        };

        CCLanes.Add(newLane);
        SelectedCCLane = newLane;
        StatusMessage = $"Added CC lane: CC{ccNumber} ({MidiCCEvent.GetControllerName(ccNumber)})";
    }

    /// <summary>
    /// Removes a CC lane.
    /// </summary>
    /// <param name="lane">The lane to remove.</param>
    [RelayCommand]
    private void RemoveCCLane(MidiCCLaneViewModel? lane)
    {
        if (lane == null) return;

        CCLanes.Remove(lane);

        if (SelectedCCLane == lane)
        {
            SelectedCCLane = CCLanes.FirstOrDefault();
        }

        StatusMessage = $"Removed CC lane: {lane.DisplayName}";
    }

    /// <summary>
    /// Removes the currently selected CC lane.
    /// </summary>
    [RelayCommand]
    private void RemoveSelectedCCLane()
    {
        RemoveCCLane(SelectedCCLane);
    }

    /// <summary>
    /// Toggles the CC lanes section visibility.
    /// </summary>
    [RelayCommand]
    private void ToggleCCLanesExpanded()
    {
        CcLanesExpanded = !CcLanesExpanded;
    }

    /// <summary>
    /// Clears all CC events for a specific controller.
    /// </summary>
    /// <param name="controller">The CC controller number.</param>
    [RelayCommand]
    private void ClearCCController(int controller)
    {
        var eventsToRemove = AllCCEvents
            .Where(e => e.Controller == controller)
            .ToList();

        foreach (var evt in eventsToRemove)
        {
            AllCCEvents.Remove(evt);
        }

        StatusMessage = $"Cleared all CC{controller} events";
    }

    /// <summary>
    /// Gets the next available CC controller number not already used by a lane.
    /// </summary>
    /// <returns>An unused CC controller number.</returns>
    private int GetNextAvailableCCController()
    {
        var usedControllers = CCLanes.Select(l => l.SelectedController).ToHashSet();

        // Try common controllers first
        int[] preferredControllers = [1, 7, 10, 11, 64, 74, 91, 93];
        foreach (var cc in preferredControllers)
        {
            if (!usedControllers.Contains(cc))
            {
                return cc;
            }
        }

        // Otherwise find any unused controller
        for (int cc = 0; cc < 128; cc++)
        {
            if (!usedControllers.Contains(cc))
            {
                return cc;
            }
        }

        return 1; // Fallback to modulation
    }

    /// <summary>
    /// Adds a CC event to the shared collection.
    /// </summary>
    /// <param name="beat">Beat position.</param>
    /// <param name="controller">CC controller number.</param>
    /// <param name="value">CC value (0-127).</param>
    /// <returns>The created CC event.</returns>
    public MidiCCEvent AddCCEvent(double beat, int controller, int value)
    {
        var evt = new MidiCCEvent(beat, controller, value);
        AllCCEvents.Add(evt);
        return evt;
    }

    /// <summary>
    /// Removes a CC event from the shared collection.
    /// </summary>
    /// <param name="evt">The event to remove.</param>
    /// <returns>True if the event was removed; otherwise false.</returns>
    public bool RemoveCCEvent(MidiCCEvent evt)
    {
        return AllCCEvents.Remove(evt);
    }

    /// <summary>
    /// Gets all CC events for a specific controller.
    /// </summary>
    /// <param name="controller">The CC controller number.</param>
    /// <returns>Collection of CC events for the controller.</returns>
    public IEnumerable<MidiCCEvent> GetCCEventsForController(int controller)
    {
        return AllCCEvents.Where(e => e.Controller == controller).OrderBy(e => e.Beat);
    }

    /// <summary>
    /// Gets the interpolated CC value at a specific beat for a controller.
    /// </summary>
    /// <param name="controller">The CC controller number.</param>
    /// <param name="beat">The beat position.</param>
    /// <returns>The interpolated value, or null if no events exist.</returns>
    public int? GetCCValueAtBeat(int controller, double beat)
    {
        var events = GetCCEventsForController(controller).ToList();

        if (events.Count == 0) return null;

        if (beat <= events[0].Beat) return events[0].Value;
        if (beat >= events[^1].Beat) return events[^1].Value;

        for (int i = 0; i < events.Count - 1; i++)
        {
            if (beat >= events[i].Beat && beat < events[i + 1].Beat)
            {
                // Linear interpolation
                double t = (beat - events[i].Beat) / (events[i + 1].Beat - events[i].Beat);
                return (int)Math.Round(events[i].Value + t * (events[i + 1].Value - events[i].Value));
            }
        }

        return events[^1].Value;
    }

    #endregion

    #region Note Preview

    /// <summary>
    /// Requests a note preview (plays the note sound).
    /// </summary>
    /// <param name="midiNote">The MIDI note number to preview.</param>
    /// <param name="velocity">The velocity for the preview.</param>
    public void RequestNotePreview(int midiNote, int velocity = 100)
    {
        if (!NotePreviewEnabled) return;

        // Play through audio engine service
        try
        {
            _audioEngineService.PlayNotePreview(midiNote, velocity);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Note preview error: {ex.Message}");
        }

        // Also raise event for any other listeners
        NotePreviewRequested?.Invoke(this, new NotePreviewEventArgs(midiNote, velocity));
    }

    /// <summary>
    /// Requests stopping a note preview.
    /// </summary>
    /// <param name="midiNote">The MIDI note number to stop.</param>
    public void RequestNotePreviewStop(int midiNote)
    {
        // Stop through audio engine service
        try
        {
            _audioEngineService.StopNotePreview(midiNote);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Note preview stop error: {ex.Message}");
        }

        // Also raise event for any other listeners
        NotePreviewRequested?.Invoke(this, new NotePreviewEventArgs(midiNote, 0, isNoteOff: true));
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

    #region IDisposable

    /// <summary>
    /// Disposes the PianoRollViewModel, cleaning up event subscriptions.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Stop any playing notes
        AllNotesOff();

        // Dispose EventBus subscriptions
        _beatSubscription?.Dispose();
        _playbackStartedSubscription?.Dispose();
        _playbackStoppedSubscription?.Dispose();
    }

    #endregion
}

/// <summary>
/// Event arguments for note preview requests.
/// </summary>
public class NotePreviewEventArgs : EventArgs
{
    /// <summary>
    /// Gets the MIDI note number (0-127).
    /// </summary>
    public int MidiNote { get; }

    /// <summary>
    /// Gets the velocity (0-127, 0 for note off).
    /// </summary>
    public int Velocity { get; }

    /// <summary>
    /// Gets whether this is a note-off event.
    /// </summary>
    public bool IsNoteOff { get; }

    /// <summary>
    /// Creates a new NotePreviewEventArgs.
    /// </summary>
    /// <param name="midiNote">MIDI note number.</param>
    /// <param name="velocity">Velocity value.</param>
    /// <param name="isNoteOff">Whether this is a note-off event.</param>
    public NotePreviewEventArgs(int midiNote, int velocity, bool isNoteOff = false)
    {
        MidiNote = midiNote;
        Velocity = velocity;
        IsNoteOff = isNoteOff;
    }
}

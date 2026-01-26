// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngineEditor.Models;
using MusicEngineEditor.Services;

namespace MusicEngineEditor.ViewModels;

/// <summary>
/// Defines the available tools for the Score Editor.
/// </summary>
public enum ScoreEditorTool
{
    /// <summary>Selection tool for selecting and moving notes.</summary>
    Select,

    /// <summary>Draw tool for creating new notes.</summary>
    Draw,

    /// <summary>Erase tool for deleting notes.</summary>
    Erase
}

/// <summary>
/// ViewModel for the Score Editor, managing music notation display and editing.
/// </summary>
public partial class ScoreEditorViewModel : ViewModelBase, IDisposable
{
    #region Constants

    private const double MinZoom = 0.5;
    private const double MaxZoom = 4.0;
    private const double ZoomStep = 0.25;

    private static readonly string[] KeySignatures =
    [
        "C", "G", "D", "A", "E", "B", "F#", "C#",
        "F", "Bb", "Eb", "Ab", "Db", "Gb", "Cb",
        "Am", "Em", "Bm", "F#m", "C#m", "G#m", "D#m", "A#m",
        "Dm", "Gm", "Cm", "Fm", "Bbm", "Ebm", "Abm"
    ];

    #endregion

    #region Private Fields

    private readonly PlaybackService _playbackService;
    private readonly AudioEngineService _audioEngineService;
    private EventBus.SubscriptionToken? _beatSubscription;
    private EventBus.SubscriptionToken? _playbackStartedSubscription;
    private EventBus.SubscriptionToken? _playbackStoppedSubscription;
    private bool _disposed;
    private List<ScoreNote> _clipboard = [];

    #endregion

    #region Collections

    /// <summary>
    /// Gets the collection of all notes in the score.
    /// </summary>
    public ObservableCollection<ScoreNote> Notes { get; } = [];

    /// <summary>
    /// Gets the collection of currently selected notes.
    /// </summary>
    public ObservableCollection<ScoreNote> SelectedNotes { get; } = [];

    /// <summary>
    /// Gets the available key signatures.
    /// </summary>
    public IReadOnlyList<string> AvailableKeySignatures => KeySignatures;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Gets or sets the clef type for the score.
    /// </summary>
    [ObservableProperty]
    private ClefType _clef = ClefType.Treble;

    /// <summary>
    /// Gets or sets the key signature.
    /// </summary>
    [ObservableProperty]
    private string _keySignature = "C";

    /// <summary>
    /// Gets or sets the time signature numerator (beats per measure).
    /// </summary>
    [ObservableProperty]
    private int _timeSignatureNumerator = 4;

    /// <summary>
    /// Gets or sets the time signature denominator (beat unit).
    /// </summary>
    [ObservableProperty]
    private int _timeSignatureDenominator = 4;

    /// <summary>
    /// Gets or sets the currently selected note duration for drawing.
    /// </summary>
    [ObservableProperty]
    private NoteDuration _selectedDuration = NoteDuration.Quarter;

    /// <summary>
    /// Gets or sets the zoom level.
    /// </summary>
    [ObservableProperty]
    private double _zoom = 1.0;

    /// <summary>
    /// Gets or sets the horizontal scroll position.
    /// </summary>
    [ObservableProperty]
    private double _scrollX;

    /// <summary>
    /// Gets or sets the vertical scroll position.
    /// </summary>
    [ObservableProperty]
    private double _scrollY;

    /// <summary>
    /// Gets or sets the current editing tool.
    /// </summary>
    [ObservableProperty]
    private ScoreEditorTool _currentTool = ScoreEditorTool.Select;

    /// <summary>
    /// Gets or sets the number of measures to display.
    /// </summary>
    [ObservableProperty]
    private int _measureCount = 16;

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
    /// Gets or sets whether to show measure numbers.
    /// </summary>
    [ObservableProperty]
    private bool _showMeasureNumbers = true;

    /// <summary>
    /// Gets or sets whether to show note names.
    /// </summary>
    [ObservableProperty]
    private bool _showNoteNames;

    /// <summary>
    /// Gets or sets whether dotted note mode is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isDottedMode;

    /// <summary>
    /// Gets or sets whether rest mode is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isRestMode;

    /// <summary>
    /// Gets or sets the default velocity for new notes.
    /// </summary>
    [ObservableProperty]
    private int _defaultVelocity = 100;

    /// <summary>
    /// Gets or sets whether to use a second staff (grand staff).
    /// </summary>
    [ObservableProperty]
    private bool _useGrandStaff;

    /// <summary>
    /// Gets or sets the clef type for the second staff.
    /// </summary>
    [ObservableProperty]
    private ClefType _secondStaffClef = ClefType.Bass;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets the number of beats per measure based on the time signature.
    /// </summary>
    public int BeatsPerMeasure => TimeSignatureNumerator;

    /// <summary>
    /// Gets the total number of beats in the score.
    /// </summary>
    public double TotalBeats => MeasureCount * BeatsPerMeasure;

    /// <summary>
    /// Gets whether the clipboard has content.
    /// </summary>
    public bool HasClipboardContent => _clipboard.Count > 0;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new ScoreEditorViewModel instance.
    /// </summary>
    public ScoreEditorViewModel()
    {
        _playbackService = PlaybackService.Instance;
        _audioEngineService = AudioEngineService.Instance;

        Notes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(Notes));
        SelectedNotes.CollectionChanged += (_, _) => OnPropertyChanged(nameof(SelectedNotes));

        SubscribeToPlaybackEvents();
    }

    #endregion

    #region Playback Integration

    private void SubscribeToPlaybackEvents()
    {
        var eventBus = EventBus.Instance;

        _beatSubscription = eventBus.SubscribeBeatChanged(OnBeatChanged);
        _playbackStartedSubscription = eventBus.SubscribePlaybackStarted(OnPlaybackStarted);
        _playbackStoppedSubscription = eventBus.SubscribePlaybackStopped(OnPlaybackStopped);
    }

    private void OnBeatChanged(EventBus.BeatChangedEventArgs args)
    {
        PlayheadPosition = args.CurrentBeat;
    }

    private void OnPlaybackStarted(EventBus.PlaybackStartedEventArgs args)
    {
        IsPlaying = true;
    }

    private void OnPlaybackStopped(EventBus.PlaybackStoppedEventArgs args)
    {
        IsPlaying = false;
    }

    #endregion

    #region Note Commands

    /// <summary>
    /// Adds a note to the score.
    /// </summary>
    /// <param name="note">The note to add.</param>
    [RelayCommand]
    private void AddNote(ScoreNote? note)
    {
        if (note == null) return;

        Notes.Add(note);
        StatusMessage = $"Added {note.NoteName} ({note.Duration})";
    }

    /// <summary>
    /// Deletes a note from the score.
    /// </summary>
    /// <param name="note">The note to delete.</param>
    [RelayCommand]
    private void DeleteNote(ScoreNote? note)
    {
        if (note == null) return;

        if (note.IsSelected)
        {
            SelectedNotes.Remove(note);
        }
        Notes.Remove(note);
        StatusMessage = $"Deleted {note.NoteName}";
    }

    /// <summary>
    /// Creates a new note at the specified measure and beat position.
    /// </summary>
    /// <param name="midiPitch">The MIDI pitch for the note.</param>
    /// <param name="measure">The measure number (1-based).</param>
    /// <param name="beatPosition">The beat position within the measure.</param>
    /// <returns>The created note.</returns>
    public ScoreNote CreateNote(int midiPitch, int measure, double beatPosition)
    {
        var note = new ScoreNote
        {
            MidiPitch = Math.Clamp(midiPitch, 0, 127),
            Duration = SelectedDuration,
            Measure = measure,
            BeatPosition = beatPosition,
            IsDotted = IsDottedMode,
            Velocity = DefaultVelocity
        };

        Notes.Add(note);
        return note;
    }

    /// <summary>
    /// Selects all notes in the score.
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
        StatusMessage = $"Selected {SelectedNotes.Count} note(s)";
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
        StatusMessage = $"Deleted {notesToRemove.Count} note(s)";
    }

    /// <summary>
    /// Duplicates all selected notes.
    /// </summary>
    [RelayCommand]
    private void DuplicateSelected()
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected";
            return;
        }

        var duplicates = new List<ScoreNote>();

        foreach (var note in SelectedNotes)
        {
            var duplicate = note.Clone();
            duplicate.Measure += 1; // Offset by one measure
            duplicates.Add(duplicate);
        }

        DeselectAll();

        foreach (var duplicate in duplicates)
        {
            Notes.Add(duplicate);
            duplicate.IsSelected = true;
            SelectedNotes.Add(duplicate);
        }

        StatusMessage = $"Duplicated {duplicates.Count} note(s)";
    }

    /// <summary>
    /// Transposes selected notes by the specified number of semitones.
    /// </summary>
    /// <param name="semitones">Number of semitones to transpose.</param>
    [RelayCommand]
    private void Transpose(int semitones)
    {
        if (SelectedNotes.Count == 0)
        {
            StatusMessage = "No notes selected";
            return;
        }

        foreach (var note in SelectedNotes)
        {
            note.MidiPitch = Math.Clamp(note.MidiPitch + semitones, 0, 127);
        }

        StatusMessage = $"Transposed {SelectedNotes.Count} note(s) by {semitones} semitones";
    }

    #endregion

    #region Copy/Paste Commands

    /// <summary>
    /// Copies the selected notes to the clipboard.
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

        // Find the earliest position for relative positioning
        int minMeasure = SelectedNotes.Min(n => n.Measure);
        double minBeat = SelectedNotes.Where(n => n.Measure == minMeasure).Min(n => n.BeatPosition);

        foreach (var note in SelectedNotes)
        {
            var copiedNote = note.Clone();
            copiedNote.Measure -= minMeasure - 1;
            if (note.Measure == minMeasure)
            {
                copiedNote.BeatPosition -= minBeat;
            }
            _clipboard.Add(copiedNote);
        }

        OnPropertyChanged(nameof(HasClipboardContent));
        StatusMessage = $"Copied {_clipboard.Count} note(s)";
    }

    /// <summary>
    /// Pastes notes from the clipboard at the playhead position.
    /// </summary>
    [RelayCommand]
    private void Paste()
    {
        if (_clipboard.Count == 0)
        {
            StatusMessage = "Clipboard is empty";
            return;
        }

        DeselectAll();

        // Calculate paste position from playhead
        int pasteMeasure = (int)(PlayheadPosition / BeatsPerMeasure) + 1;
        double pasteBeat = PlayheadPosition % BeatsPerMeasure;

        foreach (var clipboardNote in _clipboard)
        {
            var newNote = clipboardNote.Clone();
            newNote.Measure += pasteMeasure - 1;
            newNote.BeatPosition += pasteBeat;

            // Handle overflow to next measure
            while (newNote.BeatPosition >= BeatsPerMeasure)
            {
                newNote.BeatPosition -= BeatsPerMeasure;
                newNote.Measure++;
            }

            newNote.IsSelected = true;
            Notes.Add(newNote);
            SelectedNotes.Add(newNote);
        }

        StatusMessage = $"Pasted {_clipboard.Count} note(s)";
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
        int count = SelectedNotes.Count;
        DeleteSelected();
        StatusMessage = $"Cut {count} note(s)";
    }

    #endregion

    #region MIDI Conversion Commands

    /// <summary>
    /// Converts PianoRollNotes to ScoreNotes.
    /// </summary>
    /// <param name="midiNotes">The MIDI notes to convert.</param>
    [RelayCommand]
    private void ConvertFromMidi(IEnumerable<PianoRollNote>? midiNotes)
    {
        if (midiNotes == null) return;

        Notes.Clear();
        SelectedNotes.Clear();

        foreach (var midiNote in midiNotes)
        {
            var scoreNote = ScoreNote.FromPianoRollNote(midiNote, BeatsPerMeasure);
            Notes.Add(scoreNote);
        }

        // Adjust measure count to fit all notes
        if (Notes.Count > 0)
        {
            int maxMeasure = Notes.Max(n => n.Measure);
            if (maxMeasure > MeasureCount)
            {
                MeasureCount = maxMeasure + 4; // Add some padding
            }
        }

        StatusMessage = $"Converted {Notes.Count} notes from MIDI";
    }

    /// <summary>
    /// Exports all notes to MIDI format.
    /// </summary>
    [RelayCommand]
    private void ExportToMidi()
    {
        var pianoRollNotes = Notes.Select(n => n.ToPianoRollNote(BeatsPerMeasure)).ToList();

        // Raise an event or use a service to handle the export
        StatusMessage = $"Exported {pianoRollNotes.Count} notes to MIDI";

        // The actual MIDI export would be handled by the view or a service
        // For now, we just provide the converted notes
    }

    /// <summary>
    /// Gets all notes as PianoRollNotes for MIDI export.
    /// </summary>
    /// <returns>Collection of PianoRollNotes.</returns>
    public IEnumerable<PianoRollNote> GetMidiNotes()
    {
        return Notes.Select(n => n.ToPianoRollNote(BeatsPerMeasure));
    }

    #endregion

    #region Tool Commands

    /// <summary>
    /// Sets the current editing tool.
    /// </summary>
    /// <param name="tool">The tool to activate.</param>
    [RelayCommand]
    private void SetTool(ScoreEditorTool tool)
    {
        CurrentTool = tool;
        StatusMessage = $"Tool: {tool}";
    }

    /// <summary>
    /// Sets the selected note duration.
    /// </summary>
    /// <param name="duration">The duration to set.</param>
    [RelayCommand]
    private void SetDuration(NoteDuration duration)
    {
        SelectedDuration = duration;
        StatusMessage = $"Duration: {duration}";
    }

    /// <summary>
    /// Toggles dotted note mode.
    /// </summary>
    [RelayCommand]
    private void ToggleDotted()
    {
        IsDottedMode = !IsDottedMode;
        StatusMessage = IsDottedMode ? "Dotted mode ON" : "Dotted mode OFF";
    }

    /// <summary>
    /// Toggles rest mode.
    /// </summary>
    [RelayCommand]
    private void ToggleRestMode()
    {
        IsRestMode = !IsRestMode;
        StatusMessage = IsRestMode ? "Rest mode ON" : "Rest mode OFF";
    }

    #endregion

    #region Zoom Commands

    /// <summary>
    /// Increases the zoom level.
    /// </summary>
    [RelayCommand]
    private void ZoomIn()
    {
        Zoom = Math.Min(MaxZoom, Zoom + ZoomStep);
    }

    /// <summary>
    /// Decreases the zoom level.
    /// </summary>
    [RelayCommand]
    private void ZoomOut()
    {
        Zoom = Math.Max(MinZoom, Zoom - ZoomStep);
    }

    /// <summary>
    /// Resets the zoom to default.
    /// </summary>
    [RelayCommand]
    private void ZoomReset()
    {
        Zoom = 1.0;
    }

    /// <summary>
    /// Fits all content in view.
    /// </summary>
    [RelayCommand]
    private void ZoomToFit()
    {
        // Calculate optimal zoom based on content
        // This would be implemented based on the view's actual dimensions
        StatusMessage = "Zoom to fit";
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
    private void Pause()
    {
        _playbackService.Pause();
    }

    /// <summary>
    /// Stops playback and resets position.
    /// </summary>
    [RelayCommand]
    private void Stop()
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
    /// Jumps to the specified measure.
    /// </summary>
    /// <param name="measure">The measure number to jump to.</param>
    [RelayCommand]
    private void JumpToMeasure(int measure)
    {
        double beat = (measure - 1) * BeatsPerMeasure;
        _playbackService.SetPosition(beat);
        PlayheadPosition = beat;
    }

    #endregion

    #region Property Change Handlers

    partial void OnZoomChanged(double value)
    {
        Zoom = Math.Clamp(value, MinZoom, MaxZoom);
    }

    partial void OnTimeSignatureNumeratorChanged(int value)
    {
        OnPropertyChanged(nameof(BeatsPerMeasure));
        OnPropertyChanged(nameof(TotalBeats));
    }

    partial void OnMeasureCountChanged(int value)
    {
        OnPropertyChanged(nameof(TotalBeats));
    }

    partial void OnClefChanged(ClefType value)
    {
        StatusMessage = $"Clef changed to {value}";
    }

    partial void OnKeySignatureChanged(string value)
    {
        StatusMessage = $"Key signature: {value}";
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Gets the note at the specified measure and beat position.
    /// </summary>
    /// <param name="measure">The measure number.</param>
    /// <param name="beatPosition">The beat position within the measure.</param>
    /// <param name="midiPitch">The MIDI pitch to match (optional).</param>
    /// <returns>The note at the position, or null if none found.</returns>
    public ScoreNote? GetNoteAtPosition(int measure, double beatPosition, int? midiPitch = null)
    {
        return Notes.FirstOrDefault(n =>
            n.Measure == measure &&
            beatPosition >= n.BeatPosition &&
            beatPosition < n.BeatPosition + n.DurationInBeats &&
            (midiPitch == null || n.MidiPitch == midiPitch));
    }

    /// <summary>
    /// Gets all notes in the specified measure.
    /// </summary>
    /// <param name="measure">The measure number.</param>
    /// <returns>Collection of notes in the measure.</returns>
    public IEnumerable<ScoreNote> GetNotesInMeasure(int measure)
    {
        return Notes.Where(n => n.Measure == measure).OrderBy(n => n.BeatPosition);
    }

    /// <summary>
    /// Gets notes for a specific voice.
    /// </summary>
    /// <param name="voice">The voice number (1-4).</param>
    /// <returns>Collection of notes for the voice.</returns>
    public IEnumerable<ScoreNote> GetNotesForVoice(int voice)
    {
        return Notes.Where(n => n.Voice == voice).OrderBy(n => n.GetTotalBeatPosition(BeatsPerMeasure));
    }

    /// <summary>
    /// Adds a measure at the end of the score.
    /// </summary>
    [RelayCommand]
    private void AddMeasure()
    {
        MeasureCount++;
        StatusMessage = $"Added measure. Total: {MeasureCount}";
    }

    /// <summary>
    /// Removes the last measure if it's empty.
    /// </summary>
    [RelayCommand]
    private void RemoveLastMeasure()
    {
        if (MeasureCount <= 1)
        {
            StatusMessage = "Cannot remove the only measure";
            return;
        }

        var notesInLastMeasure = GetNotesInMeasure(MeasureCount).ToList();
        if (notesInLastMeasure.Count > 0)
        {
            StatusMessage = "Cannot remove measure with notes";
            return;
        }

        MeasureCount--;
        StatusMessage = $"Removed measure. Total: {MeasureCount}";
    }

    /// <summary>
    /// Clears all notes from the score.
    /// </summary>
    [RelayCommand]
    private void ClearScore()
    {
        Notes.Clear();
        SelectedNotes.Clear();
        StatusMessage = "Score cleared";
    }

    /// <summary>
    /// Loads demo notes for testing.
    /// </summary>
    public void LoadDemoNotes()
    {
        // Add a C major scale
        int[] cMajorScale = [60, 62, 64, 65, 67, 69, 71, 72];

        for (int i = 0; i < cMajorScale.Length; i++)
        {
            Notes.Add(new ScoreNote
            {
                MidiPitch = cMajorScale[i],
                Duration = NoteDuration.Quarter,
                Measure = (i / 4) + 1,
                BeatPosition = i % 4,
                Velocity = 80
            });
        }

        // Add some half notes
        Notes.Add(new ScoreNote { MidiPitch = 60, Duration = NoteDuration.Half, Measure = 3, BeatPosition = 0, Velocity = 100 });
        Notes.Add(new ScoreNote { MidiPitch = 64, Duration = NoteDuration.Half, Measure = 3, BeatPosition = 0, Velocity = 100 });
        Notes.Add(new ScoreNote { MidiPitch = 67, Duration = NoteDuration.Half, Measure = 3, BeatPosition = 0, Velocity = 100 });

        Notes.Add(new ScoreNote { MidiPitch = 65, Duration = NoteDuration.Half, Measure = 3, BeatPosition = 2, Velocity = 90 });
        Notes.Add(new ScoreNote { MidiPitch = 69, Duration = NoteDuration.Half, Measure = 3, BeatPosition = 2, Velocity = 90 });

        // Add a whole note
        Notes.Add(new ScoreNote { MidiPitch = 60, Duration = NoteDuration.Whole, Measure = 4, BeatPosition = 0, Velocity = 100 });

        StatusMessage = "Demo notes loaded";
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the ScoreEditorViewModel, cleaning up event subscriptions.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _beatSubscription?.Dispose();
        _playbackStartedSubscription?.Dispose();
        _playbackStoppedSubscription?.Dispose();

        GC.SuppressFinalize(this);
    }

    #endregion
}

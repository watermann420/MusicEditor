// MusicEngineEditor - Chord Stamp Service
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors
// Description: Service for stamping chords in the Piano Roll with undo support.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using MusicEngine.Core.UndoRedo;
using MusicEngineEditor.Commands;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for stamping chords into the Piano Roll editor with configurable
/// root note, chord type, inversion, and voicing. Integrates with undo system.
/// </summary>
public sealed class ChordStampService : INotifyPropertyChanged, IDisposable
{
    #region Singleton

    private static ChordStampService? _instance;
    private static readonly object _lock = new();

    /// <summary>
    /// Gets the singleton instance of ChordStampService.
    /// </summary>
    public static ChordStampService Instance
    {
        get
        {
            if (_instance == null)
            {
                lock (_lock)
                {
                    _instance ??= new ChordStampService();
                }
            }
            return _instance;
        }
    }

    #endregion

    #region Private Fields

    private ChordDefinition _currentChord = ChordDefinition.Major;
    private int _currentRoot = 60; // C4
    private int _currentInversion;
    private ChordVoicing _currentVoicing = ChordVoicing.Close;
    private int _defaultVelocity = 100;
    private double _defaultDuration = 1.0;
    private bool _previewOnSelect = true;
    private bool _disposed;

    #endregion

    #region Properties

    /// <summary>
    /// Gets or sets the current chord type.
    /// </summary>
    public ChordDefinition CurrentChord
    {
        get => _currentChord;
        set
        {
            if (_currentChord != value)
            {
                _currentChord = value;
                // Clamp inversion to valid range for new chord
                _currentInversion = Math.Clamp(_currentInversion, 0, _currentChord.MaxInversion);
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentChordNotes));
                OnPropertyChanged(nameof(CurrentChordDisplay));
            }
        }
    }

    /// <summary>
    /// Gets or sets the root MIDI note (0-127).
    /// </summary>
    public int CurrentRoot
    {
        get => _currentRoot;
        set
        {
            value = Math.Clamp(value, 0, 127);
            if (_currentRoot != value)
            {
                _currentRoot = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentRootPitchClass));
                OnPropertyChanged(nameof(CurrentChordNotes));
                OnPropertyChanged(nameof(CurrentChordDisplay));
            }
        }
    }

    /// <summary>
    /// Gets or sets the root pitch class (0-11).
    /// </summary>
    public int CurrentRootPitchClass
    {
        get => _currentRoot % 12;
        set
        {
            int octave = _currentRoot / 12;
            CurrentRoot = value + (octave * 12);
        }
    }

    /// <summary>
    /// Gets or sets the current inversion (0 = root position).
    /// </summary>
    public int CurrentInversion
    {
        get => _currentInversion;
        set
        {
            value = Math.Clamp(value, 0, _currentChord.MaxInversion);
            if (_currentInversion != value)
            {
                _currentInversion = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentChordNotes));
            }
        }
    }

    /// <summary>
    /// Gets or sets the current voicing.
    /// </summary>
    public ChordVoicing CurrentVoicing
    {
        get => _currentVoicing;
        set
        {
            if (_currentVoicing != value)
            {
                _currentVoicing = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentChordNotes));
            }
        }
    }

    /// <summary>
    /// Gets or sets the default velocity for stamped notes (0-127).
    /// </summary>
    public int DefaultVelocity
    {
        get => _defaultVelocity;
        set
        {
            value = Math.Clamp(value, 1, 127);
            if (_defaultVelocity != value)
            {
                _defaultVelocity = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the default duration for stamped notes in beats.
    /// </summary>
    public double DefaultDuration
    {
        get => _defaultDuration;
        set
        {
            value = Math.Max(0.0625, value);
            if (Math.Abs(_defaultDuration - value) > 0.001)
            {
                _defaultDuration = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether to preview the chord when selecting a new one.
    /// </summary>
    public bool PreviewOnSelect
    {
        get => _previewOnSelect;
        set
        {
            if (_previewOnSelect != value)
            {
                _previewOnSelect = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets the current chord notes as MIDI note numbers.
    /// </summary>
    public int[] CurrentChordNotes => _currentChord.GetNotes(_currentRoot, _currentInversion, _currentVoicing);

    /// <summary>
    /// Gets the display string for the current chord (e.g., "C Major").
    /// </summary>
    public string CurrentChordDisplay => _currentChord.GetFullName(_currentRoot);

    #endregion

    #region Events

    /// <summary>
    /// Raised when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raised when a chord is stamped.
    /// </summary>
    public event EventHandler<ChordStampedEventArgs>? ChordStamped;

    /// <summary>
    /// Raised when a chord preview is requested.
    /// </summary>
    public event EventHandler<ChordPreviewEventArgs>? ChordPreviewRequested;

    #endregion

    #region Constructor

    private ChordStampService()
    {
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Stamps the current chord at the specified position.
    /// </summary>
    /// <param name="notesCollection">The notes collection to add to.</param>
    /// <param name="startBeat">Start position in beats.</param>
    /// <param name="duration">Duration in beats (null = use default).</param>
    /// <param name="velocity">Velocity (null = use default).</param>
    /// <returns>List of created notes.</returns>
    public IReadOnlyList<PianoRollNote> StampChord(
        ObservableCollection<PianoRollNote> notesCollection,
        double startBeat,
        double? duration = null,
        int? velocity = null)
    {
        if (notesCollection == null)
            throw new ArgumentNullException(nameof(notesCollection));

        var effectiveDuration = duration ?? _defaultDuration;
        var effectiveVelocity = velocity ?? _defaultVelocity;

        var chordNotes = CurrentChordNotes;
        var createdNotes = new List<PianoRollNote>();

        // Create notes
        foreach (var midiNote in chordNotes)
        {
            var note = new PianoRollNote
            {
                Note = midiNote,
                StartBeat = startBeat,
                Duration = effectiveDuration,
                Velocity = effectiveVelocity
            };
            createdNotes.Add(note);
        }

        // Execute with undo support
        var undoService = EditorUndoService.Instance;
        using var batch = undoService.BeginBatch($"Stamp {_currentChord.GetSymbolWithRoot(_currentRoot)} Chord");

        foreach (var note in createdNotes)
        {
            var command = new NoteAddCommand(notesCollection, note);
            batch.Execute(command);
        }

        // Raise event
        ChordStamped?.Invoke(this, new ChordStampedEventArgs(createdNotes, _currentChord, _currentRoot));

        return createdNotes;
    }

    /// <summary>
    /// Stamps a specific chord at the specified position.
    /// </summary>
    /// <param name="notesCollection">The notes collection to add to.</param>
    /// <param name="chord">The chord to stamp.</param>
    /// <param name="rootNote">Root MIDI note.</param>
    /// <param name="startBeat">Start position in beats.</param>
    /// <param name="duration">Duration in beats.</param>
    /// <param name="velocity">Velocity.</param>
    /// <param name="inversion">Inversion number.</param>
    /// <param name="voicing">Voicing type.</param>
    /// <returns>List of created notes.</returns>
    public IReadOnlyList<PianoRollNote> StampChord(
        ObservableCollection<PianoRollNote> notesCollection,
        ChordDefinition chord,
        int rootNote,
        double startBeat,
        double duration,
        int velocity,
        int inversion = 0,
        ChordVoicing voicing = ChordVoicing.Close)
    {
        if (notesCollection == null)
            throw new ArgumentNullException(nameof(notesCollection));

        if (chord == null)
            throw new ArgumentNullException(nameof(chord));

        var chordNotes = chord.GetNotes(rootNote, inversion, voicing);
        var createdNotes = new List<PianoRollNote>();

        foreach (var midiNote in chordNotes)
        {
            var note = new PianoRollNote
            {
                Note = midiNote,
                StartBeat = startBeat,
                Duration = duration,
                Velocity = velocity
            };
            createdNotes.Add(note);
        }

        // Execute with undo support
        var undoService = EditorUndoService.Instance;
        using var batch = undoService.BeginBatch($"Stamp {chord.GetSymbolWithRoot(rootNote)} Chord");

        foreach (var note in createdNotes)
        {
            var command = new NoteAddCommand(notesCollection, note);
            batch.Execute(command);
        }

        // Raise event
        ChordStamped?.Invoke(this, new ChordStampedEventArgs(createdNotes, chord, rootNote));

        return createdNotes;
    }

    /// <summary>
    /// Previews the current chord (plays the notes).
    /// </summary>
    public void PreviewCurrentChord()
    {
        var notes = CurrentChordNotes;
        ChordPreviewRequested?.Invoke(this, new ChordPreviewEventArgs(notes, _defaultVelocity, isNoteOff: false));
    }

    /// <summary>
    /// Stops the chord preview.
    /// </summary>
    public void StopPreview()
    {
        var notes = CurrentChordNotes;
        ChordPreviewRequested?.Invoke(this, new ChordPreviewEventArgs(notes, 0, isNoteOff: true));
    }

    /// <summary>
    /// Sets the root note from a pitch class and octave.
    /// </summary>
    /// <param name="pitchClass">Pitch class (0-11).</param>
    /// <param name="octave">Octave (-1 to 9).</param>
    public void SetRoot(int pitchClass, int octave)
    {
        CurrentRoot = Math.Clamp(pitchClass + ((octave + 1) * 12), 0, 127);
    }

    /// <summary>
    /// Transposes the current root by semitones.
    /// </summary>
    /// <param name="semitones">Number of semitones (positive or negative).</param>
    public void TransposeRoot(int semitones)
    {
        CurrentRoot = Math.Clamp(_currentRoot + semitones, 0, 127);
    }

    /// <summary>
    /// Cycles to the next inversion.
    /// </summary>
    public void NextInversion()
    {
        CurrentInversion = (_currentInversion + 1) % (_currentChord.MaxInversion + 1);
    }

    /// <summary>
    /// Cycles to the previous inversion.
    /// </summary>
    public void PreviousInversion()
    {
        CurrentInversion = _currentInversion == 0 ? _currentChord.MaxInversion : _currentInversion - 1;
    }

    /// <summary>
    /// Gets a preview of what notes would be stamped without actually stamping.
    /// </summary>
    /// <param name="startBeat">Start position.</param>
    /// <param name="duration">Duration (null = use default).</param>
    /// <returns>Preview notes (not added to any collection).</returns>
    public IReadOnlyList<PianoRollNote> GetPreviewNotes(double startBeat, double? duration = null)
    {
        var effectiveDuration = duration ?? _defaultDuration;
        var chordNotes = CurrentChordNotes;

        return chordNotes.Select(midiNote => new PianoRollNote
        {
            Note = midiNote,
            StartBeat = startBeat,
            Duration = effectiveDuration,
            Velocity = _defaultVelocity
        }).ToList();
    }

    #endregion

    #region INotifyPropertyChanged

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }

    #endregion
}

/// <summary>
/// Event arguments for chord stamped events.
/// </summary>
public class ChordStampedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the created notes.
    /// </summary>
    public IReadOnlyList<PianoRollNote> Notes { get; }

    /// <summary>
    /// Gets the chord that was stamped.
    /// </summary>
    public ChordDefinition Chord { get; }

    /// <summary>
    /// Gets the root note.
    /// </summary>
    public int RootNote { get; }

    /// <summary>
    /// Creates a new ChordStampedEventArgs.
    /// </summary>
    public ChordStampedEventArgs(IReadOnlyList<PianoRollNote> notes, ChordDefinition chord, int rootNote)
    {
        Notes = notes;
        Chord = chord;
        RootNote = rootNote;
    }
}

/// <summary>
/// Event arguments for chord preview requests.
/// </summary>
public class ChordPreviewEventArgs : EventArgs
{
    /// <summary>
    /// Gets the MIDI notes to preview.
    /// </summary>
    public int[] Notes { get; }

    /// <summary>
    /// Gets the velocity.
    /// </summary>
    public int Velocity { get; }

    /// <summary>
    /// Gets whether this is a note-off event.
    /// </summary>
    public bool IsNoteOff { get; }

    /// <summary>
    /// Creates a new ChordPreviewEventArgs.
    /// </summary>
    public ChordPreviewEventArgs(int[] notes, int velocity, bool isNoteOff)
    {
        Notes = notes;
        Velocity = velocity;
        IsNoteOff = isNoteOff;
    }
}

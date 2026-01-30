// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Note editing commands for piano roll.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using MusicEngine.Core.UndoRedo;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Commands;

/// <summary>
/// Command for adding a note to the piano roll.
/// </summary>
public sealed class NoteAddCommand : IUndoableCommand
{
    private readonly ObservableCollection<PianoRollNote> _notes;
    private readonly PianoRollNote _note;

    /// <inheritdoc/>
    public string Description => $"Add Note {_note.NoteName}";

    /// <summary>
    /// Creates a new NoteAddCommand.
    /// </summary>
    /// <param name="notes">The notes collection to add to.</param>
    /// <param name="note">The note to add.</param>
    public NoteAddCommand(ObservableCollection<PianoRollNote> notes, PianoRollNote note)
    {
        _notes = notes ?? throw new ArgumentNullException(nameof(notes));
        _note = note ?? throw new ArgumentNullException(nameof(note));
    }

    /// <inheritdoc/>
    public void Execute() => _notes.Add(_note);

    /// <inheritdoc/>
    public void Undo() => _notes.Remove(_note);
}

/// <summary>
/// Command for deleting one or more notes from the piano roll.
/// </summary>
public sealed class NoteDeleteCommand : IUndoableCommand
{
    private readonly ObservableCollection<PianoRollNote> _notes;
    private readonly List<NoteSnapshot> _deletedNotes;

    /// <inheritdoc/>
    public string Description => _deletedNotes.Count == 1
        ? $"Delete Note {_deletedNotes[0].NoteName}"
        : $"Delete {_deletedNotes.Count} Notes";

    /// <summary>
    /// Creates a new NoteDeleteCommand for a single note.
    /// </summary>
    /// <param name="notes">The notes collection.</param>
    /// <param name="noteToDelete">The note to delete.</param>
    public NoteDeleteCommand(ObservableCollection<PianoRollNote> notes, PianoRollNote noteToDelete)
        : this(notes, new[] { noteToDelete })
    {
    }

    /// <summary>
    /// Creates a new NoteDeleteCommand for multiple notes.
    /// </summary>
    /// <param name="notes">The notes collection.</param>
    /// <param name="notesToDelete">The notes to delete.</param>
    public NoteDeleteCommand(ObservableCollection<PianoRollNote> notes, IEnumerable<PianoRollNote> notesToDelete)
    {
        _notes = notes ?? throw new ArgumentNullException(nameof(notes));
        _deletedNotes = notesToDelete.Select((n, i) => new NoteSnapshot(n, _notes.IndexOf(n))).ToList();
    }

    /// <inheritdoc/>
    public void Execute()
    {
        // Remove in reverse index order to maintain correct indices
        foreach (var snapshot in _deletedNotes.OrderByDescending(s => s.Index))
        {
            _notes.Remove(snapshot.Note);
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        // Restore in original index order
        foreach (var snapshot in _deletedNotes.OrderBy(s => s.Index))
        {
            if (snapshot.Index >= 0 && snapshot.Index <= _notes.Count)
            {
                _notes.Insert(snapshot.Index, snapshot.Note);
            }
            else
            {
                _notes.Add(snapshot.Note);
            }
        }
    }

    private readonly record struct NoteSnapshot(PianoRollNote Note, int Index)
    {
        public string NoteName => Note.NoteName;
    }
}

/// <summary>
/// Command for moving one or more notes to a new position/pitch.
/// </summary>
public sealed class NoteMoveCommand : IUndoableCommand
{
    private readonly List<NoteMoveData> _moveData;
    private readonly Guid _mergeId;

    /// <inheritdoc/>
    public string Description => _moveData.Count == 1
        ? $"Move Note {_moveData[0].Note.NoteName}"
        : $"Move {_moveData.Count} Notes";

    /// <summary>
    /// Creates a new NoteMoveCommand for a single note.
    /// </summary>
    /// <param name="note">The note to move.</param>
    /// <param name="newStartBeat">The new start beat position.</param>
    /// <param name="newPitch">The new MIDI pitch.</param>
    public NoteMoveCommand(PianoRollNote note, double newStartBeat, int newPitch)
        : this(new[] { note }, new[] { (newStartBeat, newPitch) })
    {
    }

    /// <summary>
    /// Creates a new NoteMoveCommand for multiple notes.
    /// </summary>
    /// <param name="notes">The notes to move.</param>
    /// <param name="newPositions">The new positions (startBeat, pitch) for each note.</param>
    public NoteMoveCommand(IEnumerable<PianoRollNote> notes, IEnumerable<(double startBeat, int pitch)> newPositions)
    {
        var notesList = notes.ToList();
        var positionsList = newPositions.ToList();

        if (notesList.Count != positionsList.Count)
            throw new ArgumentException("Notes and positions count must match.");

        _moveData = notesList.Zip(positionsList, (note, pos) => new NoteMoveData(
            note,
            note.StartBeat,
            note.Note,
            pos.startBeat,
            pos.pitch
        )).ToList();

        _mergeId = Guid.NewGuid();
    }

    private NoteMoveCommand(List<NoteMoveData> moveData, Guid mergeId)
    {
        _moveData = moveData;
        _mergeId = mergeId;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        foreach (var data in _moveData)
        {
            data.Note.StartBeat = data.NewStartBeat;
            data.Note.Note = data.NewPitch;
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        foreach (var data in _moveData)
        {
            data.Note.StartBeat = data.OldStartBeat;
            data.Note.Note = data.OldPitch;
        }
    }

    /// <inheritdoc/>
    public bool CanMergeWith(IUndoableCommand other)
    {
        // Allow merging with another move command on the same notes within a short time
        return other is NoteMoveCommand otherMove &&
               _moveData.Count == otherMove._moveData.Count &&
               _moveData.Zip(otherMove._moveData, (a, b) => a.Note.Id == b.Note.Id).All(x => x);
    }

    /// <inheritdoc/>
    public IUndoableCommand MergeWith(IUndoableCommand other)
    {
        if (other is NoteMoveCommand otherMove)
        {
            // Create merged command: original positions -> final positions
            var mergedData = _moveData.Zip(otherMove._moveData, (original, final) => new NoteMoveData(
                original.Note,
                original.OldStartBeat,
                original.OldPitch,
                final.NewStartBeat,
                final.NewPitch
            )).ToList();

            return new NoteMoveCommand(mergedData, _mergeId);
        }
        return this;
    }

    private readonly record struct NoteMoveData(
        PianoRollNote Note,
        double OldStartBeat,
        int OldPitch,
        double NewStartBeat,
        int NewPitch);
}

/// <summary>
/// Command for resizing (changing duration of) one or more notes.
/// </summary>
public sealed class NoteResizeCommand : IUndoableCommand
{
    private readonly List<NoteResizeData> _resizeData;

    /// <inheritdoc/>
    public string Description => _resizeData.Count == 1
        ? $"Resize Note {_resizeData[0].Note.NoteName}"
        : $"Resize {_resizeData.Count} Notes";

    /// <summary>
    /// Creates a new NoteResizeCommand for a single note.
    /// </summary>
    /// <param name="note">The note to resize.</param>
    /// <param name="newDuration">The new duration in beats.</param>
    public NoteResizeCommand(PianoRollNote note, double newDuration)
        : this(new[] { note }, new[] { newDuration })
    {
    }

    /// <summary>
    /// Creates a new NoteResizeCommand for multiple notes.
    /// </summary>
    /// <param name="notes">The notes to resize.</param>
    /// <param name="newDurations">The new durations for each note.</param>
    public NoteResizeCommand(IEnumerable<PianoRollNote> notes, IEnumerable<double> newDurations)
    {
        var notesList = notes.ToList();
        var durationsList = newDurations.ToList();

        if (notesList.Count != durationsList.Count)
            throw new ArgumentException("Notes and durations count must match.");

        _resizeData = notesList.Zip(durationsList, (note, dur) => new NoteResizeData(
            note,
            note.Duration,
            dur
        )).ToList();
    }

    private NoteResizeCommand(List<NoteResizeData> resizeData)
    {
        _resizeData = resizeData;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        foreach (var data in _resizeData)
        {
            data.Note.Duration = data.NewDuration;
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        foreach (var data in _resizeData)
        {
            data.Note.Duration = data.OldDuration;
        }
    }

    /// <inheritdoc/>
    public bool CanMergeWith(IUndoableCommand other)
    {
        return other is NoteResizeCommand otherResize &&
               _resizeData.Count == otherResize._resizeData.Count &&
               _resizeData.Zip(otherResize._resizeData, (a, b) => a.Note.Id == b.Note.Id).All(x => x);
    }

    /// <inheritdoc/>
    public IUndoableCommand MergeWith(IUndoableCommand other)
    {
        if (other is NoteResizeCommand otherResize)
        {
            var mergedData = _resizeData.Zip(otherResize._resizeData, (original, final) => new NoteResizeData(
                original.Note,
                original.OldDuration,
                final.NewDuration
            )).ToList();

            return new NoteResizeCommand(mergedData);
        }
        return this;
    }

    private readonly record struct NoteResizeData(
        PianoRollNote Note,
        double OldDuration,
        double NewDuration);
}

/// <summary>
/// Command for changing the velocity of one or more notes.
/// </summary>
public sealed class NoteVelocityCommand : IUndoableCommand
{
    private readonly List<NoteVelocityData> _velocityData;

    /// <inheritdoc/>
    public string Description => _velocityData.Count == 1
        ? $"Change Velocity to {_velocityData[0].NewVelocity}"
        : $"Change Velocity of {_velocityData.Count} Notes";

    /// <summary>
    /// Creates a new NoteVelocityCommand for a single note.
    /// </summary>
    /// <param name="note">The note to modify.</param>
    /// <param name="newVelocity">The new velocity value (0-127).</param>
    public NoteVelocityCommand(PianoRollNote note, int newVelocity)
        : this(new[] { note }, new[] { newVelocity })
    {
    }

    /// <summary>
    /// Creates a new NoteVelocityCommand for multiple notes with the same velocity.
    /// </summary>
    /// <param name="notes">The notes to modify.</param>
    /// <param name="newVelocity">The new velocity value (0-127).</param>
    public NoteVelocityCommand(IEnumerable<PianoRollNote> notes, int newVelocity)
        : this(notes, notes.Select(_ => newVelocity))
    {
    }

    /// <summary>
    /// Creates a new NoteVelocityCommand for multiple notes with individual velocities.
    /// </summary>
    /// <param name="notes">The notes to modify.</param>
    /// <param name="newVelocities">The new velocities for each note.</param>
    public NoteVelocityCommand(IEnumerable<PianoRollNote> notes, IEnumerable<int> newVelocities)
    {
        var notesList = notes.ToList();
        var velocitiesList = newVelocities.ToList();

        if (notesList.Count != velocitiesList.Count)
            throw new ArgumentException("Notes and velocities count must match.");

        _velocityData = notesList.Zip(velocitiesList, (note, vel) => new NoteVelocityData(
            note,
            note.Velocity,
            Math.Clamp(vel, 0, 127)
        )).ToList();
    }

    private NoteVelocityCommand(List<NoteVelocityData> velocityData)
    {
        _velocityData = velocityData;
    }

    /// <inheritdoc/>
    public void Execute()
    {
        foreach (var data in _velocityData)
        {
            data.Note.Velocity = data.NewVelocity;
        }
    }

    /// <inheritdoc/>
    public void Undo()
    {
        foreach (var data in _velocityData)
        {
            data.Note.Velocity = data.OldVelocity;
        }
    }

    /// <inheritdoc/>
    public bool CanMergeWith(IUndoableCommand other)
    {
        return other is NoteVelocityCommand otherVel &&
               _velocityData.Count == otherVel._velocityData.Count &&
               _velocityData.Zip(otherVel._velocityData, (a, b) => a.Note.Id == b.Note.Id).All(x => x);
    }

    /// <inheritdoc/>
    public IUndoableCommand MergeWith(IUndoableCommand other)
    {
        if (other is NoteVelocityCommand otherVel)
        {
            var mergedData = _velocityData.Zip(otherVel._velocityData, (original, final) => new NoteVelocityData(
                original.Note,
                original.OldVelocity,
                final.NewVelocity
            )).ToList();

            return new NoteVelocityCommand(mergedData);
        }
        return this;
    }

    private readonly record struct NoteVelocityData(
        PianoRollNote Note,
        int OldVelocity,
        int NewVelocity);
}

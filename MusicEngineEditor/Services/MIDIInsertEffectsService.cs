// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Service implementation.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Services;

/// <summary>
/// Service for real-time MIDI processing with an effects chain.
/// Processes incoming MIDI events through arpeggiators, chord generators, transposers, etc.
/// </summary>
public sealed partial class MIDIInsertEffectsService : ObservableObject, IDisposable
{
    #region Singleton

    private static readonly Lazy<MIDIInsertEffectsService> _instance = new(
        () => new MIDIInsertEffectsService(),
        LazyThreadSafetyMode.ExecutionAndPublication);

    /// <summary>
    /// Gets the singleton instance.
    /// </summary>
    public static MIDIInsertEffectsService Instance => _instance.Value;

    #endregion

    #region Observable Properties

    /// <summary>
    /// Whether the effects chain is bypassed.
    /// </summary>
    [ObservableProperty]
    private bool _bypassed;

    /// <summary>
    /// Whether to record processed (true) or raw (false) MIDI.
    /// </summary>
    [ObservableProperty]
    private bool _recordProcessed = true;

    /// <summary>
    /// Whether the service is currently processing.
    /// </summary>
    [ObservableProperty]
    private bool _isProcessing;

    #endregion

    #region Properties

    /// <summary>
    /// Gets the effects chain.
    /// </summary>
    public ObservableCollection<IMIDIInsertEffect> EffectsChain { get; } = [];

    #endregion

    #region Events

    /// <summary>
    /// Occurs when a processed MIDI note should be output.
    /// </summary>
    public event EventHandler<MIDINoteEventArgs>? NoteOutput;

    /// <summary>
    /// Occurs when a processed MIDI CC event should be output.
    /// </summary>
    public event EventHandler<MIDICCEventArgs>? CCOutput;

    /// <summary>
    /// Occurs when the effects chain changes.
    /// </summary>
    public event EventHandler? EffectsChanged;

    #endregion

    #region Private Fields

    private readonly object _lock = new();
    private bool _disposed;
    private readonly List<MIDINoteEvent> _heldNotes = [];

    #endregion

    #region Constructor

    private MIDIInsertEffectsService()
    {
        EffectsChain.CollectionChanged += (_, _) => EffectsChanged?.Invoke(this, EventArgs.Empty);
    }

    #endregion

    #region Effect Chain Management

    /// <summary>
    /// Adds an effect to the chain.
    /// </summary>
    /// <param name="effect">The effect to add.</param>
    public void AddEffect(IMIDIInsertEffect effect)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            EffectsChain.Add(effect);
        }
    }

    /// <summary>
    /// Removes an effect from the chain.
    /// </summary>
    /// <param name="effect">The effect to remove.</param>
    public void RemoveEffect(IMIDIInsertEffect effect)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            EffectsChain.Remove(effect);
        }
    }

    /// <summary>
    /// Moves an effect in the chain.
    /// </summary>
    /// <param name="oldIndex">Current index.</param>
    /// <param name="newIndex">New index.</param>
    public void MoveEffect(int oldIndex, int newIndex)
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            if (oldIndex >= 0 && oldIndex < EffectsChain.Count &&
                newIndex >= 0 && newIndex < EffectsChain.Count)
            {
                EffectsChain.Move(oldIndex, newIndex);
            }
        }
    }

    /// <summary>
    /// Clears all effects from the chain.
    /// </summary>
    public void ClearEffects()
    {
        ThrowIfDisposed();
        lock (_lock)
        {
            EffectsChain.Clear();
        }
    }

    /// <summary>
    /// Creates and adds a transpose effect.
    /// </summary>
    /// <param name="semitones">Semitones to transpose.</param>
    /// <returns>The created effect.</returns>
    public TransposeEffect AddTranspose(int semitones = 0)
    {
        var effect = new TransposeEffect { Semitones = semitones };
        AddEffect(effect);
        return effect;
    }

    /// <summary>
    /// Creates and adds an arpeggiator effect.
    /// </summary>
    /// <returns>The created effect.</returns>
    public ArpeggiatorEffect AddArpeggiator()
    {
        var effect = new ArpeggiatorEffect();
        AddEffect(effect);
        return effect;
    }

    /// <summary>
    /// Creates and adds a chord effect.
    /// </summary>
    /// <returns>The created effect.</returns>
    public ChordEffect AddChord()
    {
        var effect = new ChordEffect();
        AddEffect(effect);
        return effect;
    }

    /// <summary>
    /// Creates and adds a velocity curve effect.
    /// </summary>
    /// <returns>The created effect.</returns>
    public VelocityCurveEffect AddVelocityCurve()
    {
        var effect = new VelocityCurveEffect();
        AddEffect(effect);
        return effect;
    }

    #endregion

    #region MIDI Processing

    /// <summary>
    /// Processes a MIDI Note On event through the effects chain.
    /// </summary>
    /// <param name="noteNumber">MIDI note number (0-127).</param>
    /// <param name="velocity">Note velocity (1-127).</param>
    /// <param name="channel">MIDI channel (0-15).</param>
    public void ProcessNoteOn(int noteNumber, int velocity, int channel = 0)
    {
        ThrowIfDisposed();

        if (Bypassed)
        {
            // Output raw note
            NoteOutput?.Invoke(this, new MIDINoteEventArgs(noteNumber, velocity, channel, true));
            return;
        }

        lock (_lock)
        {
            IsProcessing = true;

            var inputEvent = new MIDINoteEvent
            {
                NoteNumber = noteNumber,
                Velocity = velocity,
                Channel = channel,
                IsNoteOn = true
            };

            _heldNotes.Add(inputEvent);

            // Process through effects chain
            var outputEvents = ProcessThroughChain([inputEvent]);

            // Output processed events
            foreach (var evt in outputEvents)
            {
                NoteOutput?.Invoke(this, new MIDINoteEventArgs(
                    evt.NoteNumber, evt.Velocity, evt.Channel, evt.IsNoteOn));
            }

            IsProcessing = false;
        }
    }

    /// <summary>
    /// Processes a MIDI Note Off event through the effects chain.
    /// </summary>
    /// <param name="noteNumber">MIDI note number (0-127).</param>
    /// <param name="channel">MIDI channel (0-15).</param>
    public void ProcessNoteOff(int noteNumber, int channel = 0)
    {
        ThrowIfDisposed();

        if (Bypassed)
        {
            NoteOutput?.Invoke(this, new MIDINoteEventArgs(noteNumber, 0, channel, false));
            return;
        }

        lock (_lock)
        {
            // Remove from held notes
            _heldNotes.RemoveAll(n => n.NoteNumber == noteNumber && n.Channel == channel);

            var inputEvent = new MIDINoteEvent
            {
                NoteNumber = noteNumber,
                Velocity = 0,
                Channel = channel,
                IsNoteOn = false
            };

            // Process through effects chain
            var outputEvents = ProcessThroughChain([inputEvent]);

            // Output processed events
            foreach (var evt in outputEvents)
            {
                NoteOutput?.Invoke(this, new MIDINoteEventArgs(
                    evt.NoteNumber, evt.Velocity, evt.Channel, evt.IsNoteOn));
            }
        }
    }

    /// <summary>
    /// Processes a MIDI CC event through the effects chain.
    /// </summary>
    /// <param name="controller">CC controller number (0-127).</param>
    /// <param name="value">CC value (0-127).</param>
    /// <param name="channel">MIDI channel (0-15).</param>
    public void ProcessCC(int controller, int value, int channel = 0)
    {
        ThrowIfDisposed();

        if (Bypassed)
        {
            CCOutput?.Invoke(this, new MIDICCEventArgs(controller, value, channel));
            return;
        }

        lock (_lock)
        {
            var inputEvent = new MIDICCEvent
            {
                Controller = controller,
                Value = value,
                Channel = channel
            };

            // Process through effects chain
            foreach (var effect in EffectsChain.Where(e => e.IsEnabled))
            {
                inputEvent = effect.ProcessCC(inputEvent);
            }

            CCOutput?.Invoke(this, new MIDICCEventArgs(
                inputEvent.Controller, inputEvent.Value, inputEvent.Channel));
        }
    }

    private List<MIDINoteEvent> ProcessThroughChain(List<MIDINoteEvent> events)
    {
        var currentEvents = events;

        foreach (var effect in EffectsChain.Where(e => e.IsEnabled))
        {
            var processedEvents = new List<MIDINoteEvent>();

            foreach (var evt in currentEvents)
            {
                var outputs = effect.ProcessNote(evt);
                processedEvents.AddRange(outputs);
            }

            currentEvents = processedEvents;
        }

        return currentEvents;
    }

    /// <summary>
    /// Sends all notes off to stop any hanging notes.
    /// </summary>
    public void AllNotesOff()
    {
        lock (_lock)
        {
            foreach (var note in _heldNotes.ToList())
            {
                NoteOutput?.Invoke(this, new MIDINoteEventArgs(
                    note.NoteNumber, 0, note.Channel, false));
            }
            _heldNotes.Clear();

            // Also notify effects
            foreach (var effect in EffectsChain)
            {
                effect.Reset();
            }
        }
    }

    #endregion

    #region Private Methods

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(MIDIInsertEffectsService));
        }
    }

    #endregion

    #region IDisposable

    /// <summary>
    /// Disposes the service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            AllNotesOff();
            EffectsChain.Clear();
        }
    }

    #endregion
}

#region Interfaces

/// <summary>
/// Interface for MIDI insert effects.
/// </summary>
public interface IMIDIInsertEffect
{
    /// <summary>
    /// Gets or sets whether the effect is enabled.
    /// </summary>
    bool IsEnabled { get; set; }

    /// <summary>
    /// Gets the display name of the effect.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Processes a MIDI note event.
    /// </summary>
    /// <param name="input">The input event.</param>
    /// <returns>The output events (may be multiple for chord/arp).</returns>
    List<MIDINoteEvent> ProcessNote(MIDINoteEvent input);

    /// <summary>
    /// Processes a MIDI CC event.
    /// </summary>
    /// <param name="input">The input event.</param>
    /// <returns>The output event.</returns>
    MIDICCEvent ProcessCC(MIDICCEvent input);

    /// <summary>
    /// Resets the effect state.
    /// </summary>
    void Reset();
}

#endregion

#region Event Types

/// <summary>
/// Represents a MIDI note event for processing.
/// </summary>
public class MIDINoteEvent
{
    public int NoteNumber { get; set; }
    public int Velocity { get; set; }
    public int Channel { get; set; }
    public bool IsNoteOn { get; set; }
}

/// <summary>
/// Represents a MIDI CC event for processing.
/// </summary>
public class MIDICCEvent
{
    public int Controller { get; set; }
    public int Value { get; set; }
    public int Channel { get; set; }
}

/// <summary>
/// Event arguments for MIDI note output.
/// </summary>
public sealed class MIDINoteEventArgs : EventArgs
{
    public int NoteNumber { get; }
    public int Velocity { get; }
    public int Channel { get; }
    public bool IsNoteOn { get; }

    public MIDINoteEventArgs(int noteNumber, int velocity, int channel, bool isNoteOn)
    {
        NoteNumber = noteNumber;
        Velocity = velocity;
        Channel = channel;
        IsNoteOn = isNoteOn;
    }
}

/// <summary>
/// Event arguments for MIDI CC output.
/// </summary>
public sealed class MIDICCEventArgs : EventArgs
{
    public int Controller { get; }
    public int Value { get; }
    public int Channel { get; }

    public MIDICCEventArgs(int controller, int value, int channel)
    {
        Controller = controller;
        Value = value;
        Channel = channel;
    }
}

#endregion

#region Built-in Effects

/// <summary>
/// Transposes MIDI notes by a number of semitones.
/// </summary>
public class TransposeEffect : ObservableObject, IMIDIInsertEffect
{
    private bool _isEnabled = true;
    private int _semitones;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string Name => "Transpose";

    /// <summary>
    /// Gets or sets the number of semitones to transpose.
    /// </summary>
    public int Semitones
    {
        get => _semitones;
        set => SetProperty(ref _semitones, Math.Clamp(value, -48, 48));
    }

    public List<MIDINoteEvent> ProcessNote(MIDINoteEvent input)
    {
        return
        [
            new MIDINoteEvent
            {
                NoteNumber = Math.Clamp(input.NoteNumber + Semitones, 0, 127),
                Velocity = input.Velocity,
                Channel = input.Channel,
                IsNoteOn = input.IsNoteOn
            }
        ];
    }

    public MIDICCEvent ProcessCC(MIDICCEvent input) => input;
    public void Reset() { }
}

/// <summary>
/// Generates chord notes from single input notes.
/// </summary>
public class ChordEffect : ObservableObject, IMIDIInsertEffect
{
    private bool _isEnabled = true;
    private ChordType _chordType = ChordType.Major;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string Name => "Chord";

    /// <summary>
    /// Gets or sets the chord type to generate.
    /// </summary>
    public ChordType ChordType
    {
        get => _chordType;
        set => SetProperty(ref _chordType, value);
    }

    public List<MIDINoteEvent> ProcessNote(MIDINoteEvent input)
    {
        var intervals = GetChordIntervals(ChordType);
        var result = new List<MIDINoteEvent>();

        foreach (var interval in intervals)
        {
            var noteNumber = input.NoteNumber + interval;
            if (noteNumber >= 0 && noteNumber <= 127)
            {
                result.Add(new MIDINoteEvent
                {
                    NoteNumber = noteNumber,
                    Velocity = input.Velocity,
                    Channel = input.Channel,
                    IsNoteOn = input.IsNoteOn
                });
            }
        }

        return result;
    }

    private static int[] GetChordIntervals(ChordType type)
    {
        return type switch
        {
            ChordType.Major => [0, 4, 7],
            ChordType.Minor => [0, 3, 7],
            ChordType.Diminished => [0, 3, 6],
            ChordType.Augmented => [0, 4, 8],
            ChordType.Major7 => [0, 4, 7, 11],
            ChordType.Minor7 => [0, 3, 7, 10],
            ChordType.Dominant7 => [0, 4, 7, 10],
            ChordType.Sus2 => [0, 2, 7],
            ChordType.Sus4 => [0, 5, 7],
            ChordType.Power => [0, 7],
            ChordType.Octave => [0, 12],
            _ => [0]
        };
    }

    public MIDICCEvent ProcessCC(MIDICCEvent input) => input;
    public void Reset() { }
}

/// <summary>
/// Arpeggiator effect that sequences held notes.
/// </summary>
public class ArpeggiatorEffect : ObservableObject, IMIDIInsertEffect
{
    private bool _isEnabled = true;
    private ArpMode _mode = ArpMode.Up;
    private int _octaves = 1;
    private double _rate = 0.25; // Quarter notes
    private double _gate = 0.8; // Gate percentage (0-1)
    private int _velocity = -1; // -1 = use original velocity, 0-127 = fixed velocity
    private bool _latch;
    private bool _ascending = true; // For UpDown/DownUp patterns

    private readonly List<int> _heldNotes = [];
    private readonly List<int> _orderNotes = []; // Notes in order of pressing
    private int _currentStep;
    private int _lastPlayedNote = -1;
    private int _lastPlayedVelocity = 100;

    /// <summary>
    /// Occurs when the current step changes.
    /// </summary>
    public event EventHandler<ArpStepChangedEventArgs>? StepChanged;

    /// <summary>
    /// Occurs when a note should be triggered by the arpeggiator.
    /// </summary>
    public event EventHandler<ArpNoteTriggeredEventArgs>? NoteTriggered;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string Name => "Arpeggiator";

    /// <summary>
    /// Gets or sets the arpeggiator mode.
    /// </summary>
    public ArpMode Mode
    {
        get => _mode;
        set
        {
            if (SetProperty(ref _mode, value))
            {
                // Reset direction when mode changes
                _ascending = value != ArpMode.DownUp;
            }
        }
    }

    /// <summary>
    /// Gets or sets the number of octaves to span.
    /// </summary>
    public int Octaves
    {
        get => _octaves;
        set => SetProperty(ref _octaves, Math.Clamp(value, 1, 4));
    }

    /// <summary>
    /// Gets or sets the rate in beats.
    /// </summary>
    public double Rate
    {
        get => _rate;
        set => SetProperty(ref _rate, Math.Max(0.0625, value));
    }

    /// <summary>
    /// Gets or sets the gate percentage (0.0 to 1.0).
    /// </summary>
    public double Gate
    {
        get => _gate;
        set => SetProperty(ref _gate, Math.Clamp(value, 0.1, 1.0));
    }

    /// <summary>
    /// Gets or sets the velocity (-1 for original, 0-127 for fixed).
    /// </summary>
    public int Velocity
    {
        get => _velocity;
        set => SetProperty(ref _velocity, Math.Clamp(value, -1, 127));
    }

    /// <summary>
    /// Gets or sets whether latch mode is enabled (notes keep playing after release).
    /// </summary>
    public bool Latch
    {
        get => _latch;
        set => SetProperty(ref _latch, value);
    }

    /// <summary>
    /// Gets the current step index in the arpeggio sequence (0-based).
    /// </summary>
    public int CurrentStep
    {
        get => _currentStep;
        private set
        {
            if (SetProperty(ref _currentStep, value))
            {
                var sequence = GetArpSequence();
                StepChanged?.Invoke(this, new ArpStepChangedEventArgs(
                    value,
                    sequence.Count,
                    value < sequence.Count ? sequence[value] : -1));
            }
        }
    }

    /// <summary>
    /// Gets the total number of steps in the current arpeggio sequence.
    /// </summary>
    public int StepCount => GetArpSequence().Count;

    /// <summary>
    /// Gets whether there are notes being held.
    /// </summary>
    public bool HasNotes => _heldNotes.Count > 0;

    /// <summary>
    /// Gets the count of held notes.
    /// </summary>
    public int HeldNoteCount => _heldNotes.Count;

    /// <summary>
    /// Gets the last played note number (-1 if none).
    /// </summary>
    public int LastPlayedNote => _lastPlayedNote;

    public List<MIDINoteEvent> ProcessNote(MIDINoteEvent input)
    {
        if (input.IsNoteOn)
        {
            if (!_heldNotes.Contains(input.NoteNumber))
            {
                _heldNotes.Add(input.NoteNumber);
                _heldNotes.Sort();
                _orderNotes.Add(input.NoteNumber);
                _lastPlayedVelocity = input.Velocity;

                // Notify step count changed
                OnPropertyChanged(nameof(StepCount));
                OnPropertyChanged(nameof(HasNotes));
                OnPropertyChanged(nameof(HeldNoteCount));
            }
        }
        else
        {
            if (!Latch)
            {
                _heldNotes.Remove(input.NoteNumber);
                _orderNotes.Remove(input.NoteNumber);

                if (_heldNotes.Count == 0)
                {
                    CurrentStep = 0;
                    _ascending = Mode != ArpMode.DownUp;
                    _lastPlayedNote = -1;
                }

                // Notify step count changed
                OnPropertyChanged(nameof(StepCount));
                OnPropertyChanged(nameof(HasNotes));
                OnPropertyChanged(nameof(HeldNoteCount));
            }
        }

        // Pass through the input - actual arpeggio notes come from AdvanceStep()
        return [input];
    }

    /// <summary>
    /// Advances to the next step in the arpeggio and returns the note to play.
    /// Call this method from a timer synchronized to the transport tempo.
    /// </summary>
    /// <returns>The MIDI note events to trigger, or empty if no notes held.</returns>
    public List<MIDINoteEvent> AdvanceStep()
    {
        var sequence = GetArpSequence();
        if (sequence.Count == 0)
        {
            return [];
        }

        // Get the current note from the sequence
        int noteIndex = GetNextNoteIndex(sequence.Count);
        int noteNumber = sequence[noteIndex];

        // Update current step (triggers StepChanged event)
        CurrentStep = noteIndex;

        // Determine velocity
        int velocity = Velocity >= 0 ? Velocity : _lastPlayedVelocity;

        // Create note on event
        var noteOnEvent = new MIDINoteEvent
        {
            NoteNumber = noteNumber,
            Velocity = velocity,
            Channel = 0,
            IsNoteOn = true
        };

        // Track the played note
        _lastPlayedNote = noteNumber;
        OnPropertyChanged(nameof(LastPlayedNote));

        // Fire the note triggered event
        NoteTriggered?.Invoke(this, new ArpNoteTriggeredEventArgs(
            noteNumber,
            velocity,
            CurrentStep,
            sequence.Count,
            Gate));

        return [noteOnEvent];
    }

    /// <summary>
    /// Gets the note-off event for the last played note.
    /// Call this when the gate duration has elapsed.
    /// </summary>
    /// <returns>The note-off event, or null if no note was playing.</returns>
    public MIDINoteEvent? GetNoteOffEvent()
    {
        if (_lastPlayedNote < 0)
        {
            return null;
        }

        return new MIDINoteEvent
        {
            NoteNumber = _lastPlayedNote,
            Velocity = 0,
            Channel = 0,
            IsNoteOn = false
        };
    }

    /// <summary>
    /// Gets the next note index based on the current mode and direction.
    /// </summary>
    private int GetNextNoteIndex(int sequenceLength)
    {
        if (sequenceLength == 0) return 0;

        int index;

        switch (Mode)
        {
            case ArpMode.Up:
            case ArpMode.Down:
            case ArpMode.Order:
                // Simple sequential progression
                index = _currentStep % sequenceLength;
                _currentStep = (_currentStep + 1) % sequenceLength;
                break;

            case ArpMode.UpDown:
                index = _currentStep;
                if (_ascending)
                {
                    _currentStep++;
                    if (_currentStep >= sequenceLength)
                    {
                        _currentStep = Math.Max(0, sequenceLength - 2);
                        _ascending = false;
                    }
                }
                else
                {
                    _currentStep--;
                    if (_currentStep < 0)
                    {
                        _currentStep = Math.Min(1, sequenceLength - 1);
                        _ascending = true;
                    }
                }
                break;

            case ArpMode.DownUp:
                index = _currentStep;
                if (!_ascending)
                {
                    _currentStep++;
                    if (_currentStep >= sequenceLength)
                    {
                        _currentStep = Math.Max(0, sequenceLength - 2);
                        _ascending = true;
                    }
                }
                else
                {
                    _currentStep--;
                    if (_currentStep < 0)
                    {
                        _currentStep = Math.Min(1, sequenceLength - 1);
                        _ascending = false;
                    }
                }
                break;

            case ArpMode.Random:
                index = Random.Shared.Next(sequenceLength);
                _currentStep = index;
                break;

            default:
                index = _currentStep % sequenceLength;
                _currentStep = (_currentStep + 1) % sequenceLength;
                break;
        }

        return Math.Clamp(index, 0, sequenceLength - 1);
    }

    /// <summary>
    /// Gets the current arpeggiator sequence of notes.
    /// </summary>
    public List<int> GetArpSequence()
    {
        if (_heldNotes.Count == 0) return [];

        var sequence = new List<int>();

        // Build base note list based on mode
        List<int> baseNotes = Mode == ArpMode.Order ? [.. _orderNotes] : [.. _heldNotes];

        // Add base notes for each octave
        for (int oct = 0; oct < Octaves; oct++)
        {
            foreach (var note in baseNotes)
            {
                var octaveNote = note + (oct * 12);
                if (octaveNote <= 127)
                {
                    sequence.Add(octaveNote);
                }
            }
        }

        // Apply mode transformation
        return Mode switch
        {
            ArpMode.Up => sequence,
            ArpMode.Down => sequence.AsEnumerable().Reverse().ToList(),
            ArpMode.UpDown => sequence, // Direction handled in GetNextNoteIndex
            ArpMode.DownUp => sequence, // Direction handled in GetNextNoteIndex
            ArpMode.Random => sequence, // Randomization handled in GetNextNoteIndex
            ArpMode.Order => sequence,
            _ => sequence
        };
    }

    /// <summary>
    /// Gets the note at a specific step index.
    /// </summary>
    /// <param name="stepIndex">The step index (0-based).</param>
    /// <returns>The MIDI note number, or -1 if invalid.</returns>
    public int GetNoteAtStep(int stepIndex)
    {
        var sequence = GetArpSequence();
        if (stepIndex < 0 || stepIndex >= sequence.Count)
        {
            return -1;
        }
        return sequence[stepIndex];
    }

    /// <summary>
    /// Clears latch mode and releases all held notes.
    /// </summary>
    public void ClearLatch()
    {
        _heldNotes.Clear();
        _orderNotes.Clear();
        CurrentStep = 0;
        _ascending = Mode != ArpMode.DownUp;
        _lastPlayedNote = -1;

        OnPropertyChanged(nameof(StepCount));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HeldNoteCount));
        OnPropertyChanged(nameof(LastPlayedNote));
    }

    public MIDICCEvent ProcessCC(MIDICCEvent input) => input;

    public void Reset()
    {
        _heldNotes.Clear();
        _orderNotes.Clear();
        CurrentStep = 0;
        _ascending = Mode != ArpMode.DownUp;
        _lastPlayedNote = -1;

        OnPropertyChanged(nameof(StepCount));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(HeldNoteCount));
        OnPropertyChanged(nameof(LastPlayedNote));
    }
}

/// <summary>
/// Event arguments for arpeggiator step changes.
/// </summary>
public sealed class ArpStepChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the current step index (0-based).
    /// </summary>
    public int CurrentStep { get; }

    /// <summary>
    /// Gets the total number of steps in the sequence.
    /// </summary>
    public int TotalSteps { get; }

    /// <summary>
    /// Gets the MIDI note number at the current step (-1 if none).
    /// </summary>
    public int NoteNumber { get; }

    public ArpStepChangedEventArgs(int currentStep, int totalSteps, int noteNumber)
    {
        CurrentStep = currentStep;
        TotalSteps = totalSteps;
        NoteNumber = noteNumber;
    }
}

/// <summary>
/// Event arguments for arpeggiator note triggers.
/// </summary>
public sealed class ArpNoteTriggeredEventArgs : EventArgs
{
    /// <summary>
    /// Gets the MIDI note number.
    /// </summary>
    public int NoteNumber { get; }

    /// <summary>
    /// Gets the velocity (0-127).
    /// </summary>
    public int Velocity { get; }

    /// <summary>
    /// Gets the current step index (0-based).
    /// </summary>
    public int Step { get; }

    /// <summary>
    /// Gets the total number of steps.
    /// </summary>
    public int TotalSteps { get; }

    /// <summary>
    /// Gets the gate percentage (0.0-1.0).
    /// </summary>
    public double Gate { get; }

    public ArpNoteTriggeredEventArgs(int noteNumber, int velocity, int step, int totalSteps, double gate)
    {
        NoteNumber = noteNumber;
        Velocity = velocity;
        Step = step;
        TotalSteps = totalSteps;
        Gate = gate;
    }
}

/// <summary>
/// Applies a velocity curve to incoming notes.
/// </summary>
public class VelocityCurveEffect : ObservableObject, IMIDIInsertEffect
{
    private bool _isEnabled = true;
    private VelocityCurveType _curveType = VelocityCurveType.Linear;
    private double _scale = 1.0;
    private int _offset;
    private int _minimum = 1;
    private int _maximum = 127;

    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetProperty(ref _isEnabled, value);
    }

    public string Name => "Velocity Curve";

    /// <summary>
    /// Gets or sets the curve type.
    /// </summary>
    public VelocityCurveType CurveType
    {
        get => _curveType;
        set => SetProperty(ref _curveType, value);
    }

    /// <summary>
    /// Gets or sets the velocity scale factor.
    /// </summary>
    public double Scale
    {
        get => _scale;
        set => SetProperty(ref _scale, Math.Clamp(value, 0.1, 4.0));
    }

    /// <summary>
    /// Gets or sets the velocity offset.
    /// </summary>
    public int Offset
    {
        get => _offset;
        set => SetProperty(ref _offset, Math.Clamp(value, -127, 127));
    }

    /// <summary>
    /// Gets or sets the minimum output velocity.
    /// </summary>
    public int Minimum
    {
        get => _minimum;
        set => SetProperty(ref _minimum, Math.Clamp(value, 1, 127));
    }

    /// <summary>
    /// Gets or sets the maximum output velocity.
    /// </summary>
    public int Maximum
    {
        get => _maximum;
        set => SetProperty(ref _maximum, Math.Clamp(value, 1, 127));
    }

    public List<MIDINoteEvent> ProcessNote(MIDINoteEvent input)
    {
        if (!input.IsNoteOn)
        {
            return [input];
        }

        var velocity = ApplyCurve(input.Velocity);

        return
        [
            new MIDINoteEvent
            {
                NoteNumber = input.NoteNumber,
                Velocity = velocity,
                Channel = input.Channel,
                IsNoteOn = input.IsNoteOn
            }
        ];
    }

    private int ApplyCurve(int velocity)
    {
        double normalized = velocity / 127.0;

        // Apply curve
        var curved = CurveType switch
        {
            VelocityCurveType.Linear => normalized,
            VelocityCurveType.Soft => Math.Pow(normalized, 0.5),
            VelocityCurveType.Hard => Math.Pow(normalized, 2),
            VelocityCurveType.SCurve => (Math.Tanh((normalized - 0.5) * 4) + 1) / 2,
            VelocityCurveType.Fixed => 1.0,
            _ => normalized
        };

        // Apply scale and offset
        var result = (curved * 127.0 * Scale) + Offset;

        // Clamp to range
        return Math.Clamp((int)result, Minimum, Maximum);
    }

    public MIDICCEvent ProcessCC(MIDICCEvent input) => input;
    public void Reset() { }
}

#endregion

#region Enums

/// <summary>
/// Chord types for the chord effect.
/// </summary>
public enum ChordType
{
    Major,
    Minor,
    Diminished,
    Augmented,
    Major7,
    Minor7,
    Dominant7,
    Sus2,
    Sus4,
    Power,
    Octave
}

/// <summary>
/// Arpeggiator modes.
/// </summary>
public enum ArpMode
{
    Up,
    Down,
    UpDown,
    DownUp,
    Random,
    Order
}

/// <summary>
/// Velocity curve types.
/// </summary>
public enum VelocityCurveType
{
    Linear,
    Soft,
    Hard,
    SCurve,
    Fixed
}

#endregion

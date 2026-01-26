// MusicEngineEditor - MIDI Event ViewModel Model
// Copyright (c) 2026 MusicEngine Watermann420 and Contributors

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Defines the type of MIDI event.
/// </summary>
public enum MidiEventType
{
    NoteOn,
    NoteOff,
    ControlChange,
    ProgramChange,
    PitchBend,
    Aftertouch,
    ChannelPressure,
    SysEx
}

/// <summary>
/// ViewModel for a MIDI event in the Event List Editor.
/// </summary>
public partial class MidiEventViewModel : ObservableObject
{
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    /// <summary>
    /// Unique identifier for the event.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Position in beats.
    /// </summary>
    [ObservableProperty]
    private double _position;

    /// <summary>
    /// MIDI channel (0-15).
    /// </summary>
    [ObservableProperty]
    private int _channel;

    /// <summary>
    /// Type of MIDI event.
    /// </summary>
    [ObservableProperty]
    private MidiEventType _eventType = MidiEventType.NoteOn;

    /// <summary>
    /// Note number for Note events, or CC number for ControlChange.
    /// </summary>
    [ObservableProperty]
    private int _noteOrCC;

    /// <summary>
    /// Velocity for Note events, or value for CC events.
    /// </summary>
    [ObservableProperty]
    private int _velocityOrValue = 100;

    /// <summary>
    /// Duration in beats (for Note events).
    /// </summary>
    [ObservableProperty]
    private double _length;

    /// <summary>
    /// Indicates whether the event is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Indicates whether the event is muted.
    /// </summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Gets the display string for the note or CC.
    /// </summary>
    public string NoteOrCCDisplay => EventType switch
    {
        MidiEventType.NoteOn or MidiEventType.NoteOff => GetNoteName(NoteOrCC),
        MidiEventType.ControlChange => $"CC{NoteOrCC}",
        MidiEventType.ProgramChange => $"PC{NoteOrCC}",
        MidiEventType.PitchBend => "Pitch",
        MidiEventType.Aftertouch => GetNoteName(NoteOrCC),
        MidiEventType.ChannelPressure => "Pressure",
        _ => NoteOrCC.ToString()
    };

    /// <summary>
    /// Gets the display string for the velocity or value.
    /// </summary>
    public string VelocityOrValueDisplay => EventType switch
    {
        MidiEventType.NoteOn or MidiEventType.NoteOff => $"Vel: {VelocityOrValue}",
        MidiEventType.ControlChange => $"Val: {VelocityOrValue}",
        MidiEventType.PitchBend => $"{VelocityOrValue - 8192}", // Center at 0
        _ => VelocityOrValue.ToString()
    };

    /// <summary>
    /// Gets the position formatted as bar:beat:tick.
    /// </summary>
    public string PositionDisplay => FormatPosition(Position);

    /// <summary>
    /// Gets the length formatted as beats.
    /// </summary>
    public string LengthDisplay => EventType == MidiEventType.NoteOn ? $"{Length:F3}" : "-";

    /// <summary>
    /// Creates a new MIDI event with default values.
    /// </summary>
    public MidiEventViewModel()
    {
    }

    /// <summary>
    /// Creates a Note On event.
    /// </summary>
    public static MidiEventViewModel CreateNoteOn(double position, int note, int velocity, double length, int channel = 0)
    {
        return new MidiEventViewModel
        {
            Position = position,
            EventType = MidiEventType.NoteOn,
            NoteOrCC = Math.Clamp(note, 0, 127),
            VelocityOrValue = Math.Clamp(velocity, 1, 127),
            Length = length,
            Channel = Math.Clamp(channel, 0, 15)
        };
    }

    /// <summary>
    /// Creates a Control Change event.
    /// </summary>
    public static MidiEventViewModel CreateCC(double position, int controller, int value, int channel = 0)
    {
        return new MidiEventViewModel
        {
            Position = position,
            EventType = MidiEventType.ControlChange,
            NoteOrCC = Math.Clamp(controller, 0, 127),
            VelocityOrValue = Math.Clamp(value, 0, 127),
            Channel = Math.Clamp(channel, 0, 15)
        };
    }

    /// <summary>
    /// Creates a deep copy of this event.
    /// </summary>
    public MidiEventViewModel Clone()
    {
        return new MidiEventViewModel
        {
            Id = Guid.NewGuid(),
            Position = Position,
            Channel = Channel,
            EventType = EventType,
            NoteOrCC = NoteOrCC,
            VelocityOrValue = VelocityOrValue,
            Length = Length,
            IsSelected = false,
            IsMuted = IsMuted
        };
    }

    /// <summary>
    /// Gets the note name with octave.
    /// </summary>
    private static string GetNoteName(int midiNote)
    {
        if (midiNote < 0 || midiNote > 127)
            return "Invalid";

        int noteIndex = midiNote % 12;
        int octave = (midiNote / 12) - 1;
        return $"{NoteNames[noteIndex]}{octave}";
    }

    /// <summary>
    /// Formats position as bar:beat:tick.
    /// </summary>
    private static string FormatPosition(double beats, int beatsPerBar = 4)
    {
        int bar = (int)(beats / beatsPerBar) + 1;
        double beatInBar = (beats % beatsPerBar) + 1;
        int tick = (int)((beatInBar - (int)beatInBar) * 480);
        return $"{bar}:{(int)beatInBar}:{tick:D3}";
    }

    partial void OnEventTypeChanged(MidiEventType value)
    {
        OnPropertyChanged(nameof(NoteOrCCDisplay));
        OnPropertyChanged(nameof(VelocityOrValueDisplay));
        OnPropertyChanged(nameof(LengthDisplay));
    }

    partial void OnNoteOrCCChanged(int value)
    {
        OnPropertyChanged(nameof(NoteOrCCDisplay));
    }

    partial void OnVelocityOrValueChanged(int value)
    {
        OnPropertyChanged(nameof(VelocityOrValueDisplay));
    }

    partial void OnPositionChanged(double value)
    {
        OnPropertyChanged(nameof(PositionDisplay));
    }

    partial void OnLengthChanged(double value)
    {
        OnPropertyChanged(nameof(LengthDisplay));
    }
}

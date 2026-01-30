// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: MIDI CC event data model.

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a MIDI Control Change (CC) event for automation lanes.
/// </summary>
public partial class MidiCCEvent : ObservableObject
{
    /// <summary>
    /// Unique identifier for the CC event.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Position of the event in beats.
    /// </summary>
    [ObservableProperty]
    private double _beat;

    /// <summary>
    /// MIDI CC controller number (0-127).
    /// </summary>
    [ObservableProperty]
    private int _controller;

    /// <summary>
    /// CC value (0-127).
    /// </summary>
    [ObservableProperty]
    private int _value;

    /// <summary>
    /// Optional duration for ramp/interpolation to next value (in beats).
    /// If null or 0, the value is instantaneous (step).
    /// </summary>
    [ObservableProperty]
    private double? _duration;

    /// <summary>
    /// Indicates whether this event is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// MIDI channel (0-15).
    /// </summary>
    [ObservableProperty]
    private int _channel;

    /// <summary>
    /// Creates a new MIDI CC event.
    /// </summary>
    public MidiCCEvent()
    {
    }

    /// <summary>
    /// Creates a new MIDI CC event with specified values.
    /// </summary>
    /// <param name="beat">Position in beats.</param>
    /// <param name="controller">CC controller number (0-127).</param>
    /// <param name="value">CC value (0-127).</param>
    public MidiCCEvent(double beat, int controller, int value)
    {
        Beat = beat;
        Controller = Math.Clamp(controller, 0, 127);
        Value = Math.Clamp(value, 0, 127);
    }

    /// <summary>
    /// Creates a deep copy of this CC event.
    /// </summary>
    /// <returns>A new MidiCCEvent with the same values but a new Id.</returns>
    public MidiCCEvent Clone()
    {
        return new MidiCCEvent
        {
            Id = Guid.NewGuid(),
            Beat = Beat,
            Controller = Controller,
            Value = Value,
            Duration = Duration,
            IsSelected = false,
            Channel = Channel
        };
    }

    /// <summary>
    /// Gets the display name for a CC controller number.
    /// </summary>
    /// <param name="controller">The CC controller number (0-127).</param>
    /// <returns>The display name for the controller.</returns>
    public static string GetControllerName(int controller)
    {
        return controller switch
        {
            0 => "Bank Select",
            1 => "Modulation",
            2 => "Breath Controller",
            4 => "Foot Controller",
            5 => "Portamento Time",
            6 => "Data Entry MSB",
            7 => "Volume",
            8 => "Balance",
            10 => "Pan",
            11 => "Expression",
            12 => "Effect Control 1",
            13 => "Effect Control 2",
            64 => "Sustain Pedal",
            65 => "Portamento",
            66 => "Sostenuto",
            67 => "Soft Pedal",
            68 => "Legato Footswitch",
            69 => "Hold 2",
            70 => "Sound Controller 1",
            71 => "Sound Controller 2 (Resonance)",
            72 => "Sound Controller 3 (Release)",
            73 => "Sound Controller 4 (Attack)",
            74 => "Sound Controller 5 (Cutoff/Filter)",
            75 => "Sound Controller 6",
            76 => "Sound Controller 7",
            77 => "Sound Controller 8",
            78 => "Sound Controller 9",
            79 => "Sound Controller 10",
            91 => "Reverb Send",
            92 => "Tremolo Depth",
            93 => "Chorus Send",
            94 => "Detune Depth",
            95 => "Phaser Depth",
            120 => "All Sound Off",
            121 => "Reset All Controllers",
            123 => "All Notes Off",
            _ => $"CC {controller}"
        };
    }

    /// <summary>
    /// Gets the short display name for a CC controller.
    /// </summary>
    /// <param name="controller">The CC controller number.</param>
    /// <returns>Short display name.</returns>
    public static string GetControllerShortName(int controller)
    {
        return controller switch
        {
            1 => "Mod",
            7 => "Vol",
            10 => "Pan",
            11 => "Expr",
            64 => "Sust",
            74 => "Filter",
            91 => "Rev",
            93 => "Chor",
            _ => $"CC{controller}"
        };
    }
}

/// <summary>
/// Represents a common MIDI CC controller with its number and name.
/// </summary>
public record MidiCCController(int Number, string Name, string ShortName)
{
    /// <summary>
    /// Gets a list of commonly used MIDI CC controllers.
    /// </summary>
    public static MidiCCController[] CommonControllers { get; } =
    [
        new MidiCCController(1, "Modulation", "Mod"),
        new MidiCCController(7, "Volume", "Vol"),
        new MidiCCController(10, "Pan", "Pan"),
        new MidiCCController(11, "Expression", "Expr"),
        new MidiCCController(64, "Sustain Pedal", "Sust"),
        new MidiCCController(71, "Resonance", "Reso"),
        new MidiCCController(74, "Filter Cutoff", "Filter"),
        new MidiCCController(91, "Reverb Send", "Rev"),
        new MidiCCController(93, "Chorus Send", "Chor"),
    ];

    /// <summary>
    /// Gets the display string for this controller.
    /// </summary>
    public override string ToString() => $"CC{Number}: {Name}";
}

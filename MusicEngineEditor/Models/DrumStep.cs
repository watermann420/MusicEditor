// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Data model class.

using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace MusicEngineEditor.Models;

/// <summary>
/// Represents a single step in a drum lane.
/// </summary>
public partial class DrumStep : ObservableObject
{
    /// <summary>
    /// Unique identifier for the step.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Step index (0-based).
    /// </summary>
    [ObservableProperty]
    private int _stepIndex;

    /// <summary>
    /// Indicates whether the step is active (has a note).
    /// </summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>
    /// Velocity of the step (0-127).
    /// </summary>
    [ObservableProperty]
    private int _velocity = 100;

    /// <summary>
    /// Indicates whether the step is selected.
    /// </summary>
    [ObservableProperty]
    private bool _isSelected;

    /// <summary>
    /// Indicates whether this step is currently playing.
    /// </summary>
    [ObservableProperty]
    private bool _isPlaying;

    /// <summary>
    /// Probability of triggering (0.0 - 1.0).
    /// </summary>
    [ObservableProperty]
    private double _probability = 1.0;

    /// <summary>
    /// Optional micro-timing offset in ticks (-24 to +24).
    /// </summary>
    [ObservableProperty]
    private int _nudge;

    /// <summary>
    /// Creates a new drum step.
    /// </summary>
    public DrumStep()
    {
    }

    /// <summary>
    /// Creates a new drum step with specified values.
    /// </summary>
    /// <param name="stepIndex">Step index.</param>
    /// <param name="isActive">Whether the step is active.</param>
    /// <param name="velocity">Velocity value.</param>
    public DrumStep(int stepIndex, bool isActive = false, int velocity = 100)
    {
        StepIndex = stepIndex;
        IsActive = isActive;
        Velocity = Math.Clamp(velocity, 0, 127);
    }

    /// <summary>
    /// Toggles the active state of the step.
    /// </summary>
    public void Toggle()
    {
        IsActive = !IsActive;
    }

    /// <summary>
    /// Gets the normalized velocity (0.0 - 1.0) for display.
    /// </summary>
    public double NormalizedVelocity => Velocity / 127.0;

    /// <summary>
    /// Gets the opacity for velocity display.
    /// </summary>
    public double VelocityOpacity => IsActive ? Math.Max(0.3, NormalizedVelocity) : 0.0;
}

/// <summary>
/// Represents a drum lane (row) in the drum editor.
/// </summary>
public partial class DrumLane : ObservableObject
{
    /// <summary>
    /// Unique identifier for the lane.
    /// </summary>
    [ObservableProperty]
    private Guid _id = Guid.NewGuid();

    /// <summary>
    /// Display name of the drum instrument.
    /// </summary>
    [ObservableProperty]
    private string _name = "Kick";

    /// <summary>
    /// MIDI note number for the drum.
    /// </summary>
    [ObservableProperty]
    private int _midiNote = 36;

    /// <summary>
    /// Indicates whether the lane is muted.
    /// </summary>
    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Indicates whether the lane is soloed.
    /// </summary>
    [ObservableProperty]
    private bool _isSolo;

    /// <summary>
    /// Display color for the lane.
    /// </summary>
    [ObservableProperty]
    private string _color = "#00D9FF";

    /// <summary>
    /// Volume adjustment for the lane (0.0 - 1.0).
    /// </summary>
    [ObservableProperty]
    private double _volume = 1.0;

    /// <summary>
    /// Pan position (-1.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private double _pan;

    /// <summary>
    /// Creates a new drum lane.
    /// </summary>
    public DrumLane()
    {
    }

    /// <summary>
    /// Creates a new drum lane with specified values.
    /// </summary>
    /// <param name="name">Drum name.</param>
    /// <param name="midiNote">MIDI note number.</param>
    /// <param name="color">Display color.</param>
    public DrumLane(string name, int midiNote, string color)
    {
        Name = name;
        MidiNote = midiNote;
        Color = color;
    }

    /// <summary>
    /// Predefined drum kit instruments with GM mapping.
    /// </summary>
    public static readonly DrumLane[] StandardKit =
    {
        new("Kick", 36, "#DC143C"),
        new("Snare", 38, "#FFD700"),
        new("Closed HH", 42, "#4169E1"),
        new("Open HH", 46, "#6495ED"),
        new("Low Tom", 45, "#FF8C00"),
        new("Mid Tom", 47, "#FFA500"),
        new("Hi Tom", 50, "#FFD700"),
        new("Crash", 49, "#32CD32"),
        new("Ride", 51, "#20B2AA"),
        new("Clap", 39, "#FF69B4"),
        new("Rimshot", 37, "#DA70D6"),
        new("Cowbell", 56, "#9370DB"),
        new("Shaker", 70, "#87CEEB"),
        new("Tambourine", 54, "#98FB98"),
        new("Perc 1", 67, "#DDA0DD"),
        new("Perc 2", 68, "#FFB6C1")
    };

    /// <summary>
    /// Predefined 808 drum kit.
    /// </summary>
    public static readonly DrumLane[] Kit808 =
    {
        new("808 Kick", 36, "#FF0000"),
        new("808 Snare", 38, "#FF4500"),
        new("808 CH", 42, "#1E90FF"),
        new("808 OH", 46, "#00BFFF"),
        new("808 Clap", 39, "#FF1493"),
        new("808 Cowbell", 56, "#FFD700"),
        new("808 Clave", 75, "#32CD32"),
        new("808 Maracas", 70, "#00FA9A")
    };
}

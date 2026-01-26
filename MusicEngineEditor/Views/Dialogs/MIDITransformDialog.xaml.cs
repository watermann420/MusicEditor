// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Dialog window implementation.

using System;
using System.Collections.Generic;
using System.Windows;
using MusicEngineEditor.Models;

namespace MusicEngineEditor.Views.Dialogs;

/// <summary>
/// Dialog for batch transforming MIDI notes.
/// </summary>
public partial class MIDITransformDialog : Window
{
    #region Properties

    /// <summary>
    /// Gets the transform settings configured by the user.
    /// </summary>
    public MIDITransformSettings Settings { get; private set; } = new();

    /// <summary>
    /// Gets or sets the notes to be transformed (for preview).
    /// </summary>
    public List<PianoRollNote>? SourceNotes { get; set; }

    #endregion

    #region Events

    /// <summary>
    /// Occurs when preview is requested.
    /// </summary>
    public event EventHandler<MIDITransformSettings>? PreviewRequested;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new MIDITransformDialog.
    /// </summary>
    public MIDITransformDialog()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Creates a new MIDITransformDialog with preset settings.
    /// </summary>
    /// <param name="settings">Initial settings to apply.</param>
    public MIDITransformDialog(MIDITransformSettings settings) : this()
    {
        ApplySettings(settings);
    }

    #endregion

    #region Event Handlers

    private void TransposeOctaveDown_Click(object sender, RoutedEventArgs e)
    {
        TransposeSlider.Value = Math.Max(TransposeSlider.Minimum, TransposeSlider.Value - 12);
    }

    private void TransposeSemitoneDown_Click(object sender, RoutedEventArgs e)
    {
        TransposeSlider.Value = Math.Max(TransposeSlider.Minimum, TransposeSlider.Value - 1);
    }

    private void TransposeReset_Click(object sender, RoutedEventArgs e)
    {
        TransposeSlider.Value = 0;
    }

    private void TransposeSemitoneUp_Click(object sender, RoutedEventArgs e)
    {
        TransposeSlider.Value = Math.Min(TransposeSlider.Maximum, TransposeSlider.Value + 1);
    }

    private void TransposeOctaveUp_Click(object sender, RoutedEventArgs e)
    {
        TransposeSlider.Value = Math.Min(TransposeSlider.Maximum, TransposeSlider.Value + 12);
    }

    private void Preview_Click(object sender, RoutedEventArgs e)
    {
        BuildSettings();
        PreviewRequested?.Invoke(this, Settings);
    }

    private void Apply_Click(object sender, RoutedEventArgs e)
    {
        BuildSettings();
        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    #endregion

    #region Private Methods

    private void BuildSettings()
    {
        Settings = new MIDITransformSettings
        {
            TransposeSemitones = (int)TransposeSlider.Value,
            VelocityScale = VelocityScaleSlider.Value / 100.0,
            VelocityOffset = (int)VelocityOffsetSlider.Value,
            ClampVelocity = ClampVelocityCheck.IsChecked == true,
            LengthScale = LengthScaleSlider.Value / 100.0,
            PreserveLegato = PreserveLegato.IsChecked == true,
            TimeShiftBeats = TimeShiftSlider.Value,
            RandomizeEnabled = RandomizeEnabled.IsChecked == true,
            RandomVelocityRange = (int)RandomVelocitySlider.Value,
            RandomTimingRange = RandomTimingSlider.Value,
            RandomLengthRange = RandomLengthSlider.Value
        };
    }

    private void ApplySettings(MIDITransformSettings settings)
    {
        TransposeSlider.Value = settings.TransposeSemitones;
        VelocityScaleSlider.Value = settings.VelocityScale * 100.0;
        VelocityOffsetSlider.Value = settings.VelocityOffset;
        ClampVelocityCheck.IsChecked = settings.ClampVelocity;
        LengthScaleSlider.Value = settings.LengthScale * 100.0;
        PreserveLegato.IsChecked = settings.PreserveLegato;
        TimeShiftSlider.Value = settings.TimeShiftBeats;
        RandomizeEnabled.IsChecked = settings.RandomizeEnabled;
        RandomVelocitySlider.Value = settings.RandomVelocityRange;
        RandomTimingSlider.Value = settings.RandomTimingRange;
        RandomLengthSlider.Value = settings.RandomLengthRange;
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Applies the given transform settings to a list of notes.
    /// </summary>
    /// <param name="notes">The notes to transform.</param>
    /// <param name="settings">The transform settings.</param>
    /// <returns>The transformed notes (modifies in place).</returns>
    public static List<PianoRollNote> ApplyTransform(List<PianoRollNote> notes, MIDITransformSettings settings)
    {
        var random = new Random();

        foreach (var note in notes)
        {
            // Transpose
            note.Note = Math.Clamp(note.Note + settings.TransposeSemitones, 0, 127);

            // Velocity
            double velocity = note.Velocity;
            velocity *= settings.VelocityScale;
            velocity += settings.VelocityOffset;

            if (settings.RandomizeEnabled && settings.RandomVelocityRange > 0)
            {
                velocity += random.Next(-settings.RandomVelocityRange, settings.RandomVelocityRange + 1);
            }

            if (settings.ClampVelocity)
            {
                velocity = Math.Clamp(velocity, 1, 127);
            }
            note.Velocity = (int)Math.Round(velocity);

            // Length
            note.Duration *= settings.LengthScale;

            if (settings.RandomizeEnabled && settings.RandomLengthRange > 0)
            {
                var lengthOffset = (random.NextDouble() * 2 - 1) * settings.RandomLengthRange;
                note.Duration = Math.Max(0.01, note.Duration + lengthOffset);
            }

            // Time Shift
            note.StartBeat += settings.TimeShiftBeats;

            if (settings.RandomizeEnabled && settings.RandomTimingRange > 0)
            {
                var timingOffset = (random.NextDouble() * 2 - 1) * settings.RandomTimingRange;
                note.StartBeat = Math.Max(0, note.StartBeat + timingOffset);
            }
        }

        return notes;
    }

    #endregion
}

/// <summary>
/// Settings for MIDI transform operations.
/// </summary>
public class MIDITransformSettings
{
    /// <summary>
    /// Transpose amount in semitones.
    /// </summary>
    public int TransposeSemitones { get; set; }

    /// <summary>
    /// Velocity scale factor (1.0 = no change).
    /// </summary>
    public double VelocityScale { get; set; } = 1.0;

    /// <summary>
    /// Velocity offset (-127 to 127).
    /// </summary>
    public int VelocityOffset { get; set; }

    /// <summary>
    /// Whether to clamp velocity to 1-127 range.
    /// </summary>
    public bool ClampVelocity { get; set; } = true;

    /// <summary>
    /// Note length scale factor (1.0 = no change).
    /// </summary>
    public double LengthScale { get; set; } = 1.0;

    /// <summary>
    /// Whether to preserve legato connections.
    /// </summary>
    public bool PreserveLegato { get; set; }

    /// <summary>
    /// Time shift in beats.
    /// </summary>
    public double TimeShiftBeats { get; set; }

    /// <summary>
    /// Whether randomization is enabled.
    /// </summary>
    public bool RandomizeEnabled { get; set; }

    /// <summary>
    /// Random velocity variation range (+/-).
    /// </summary>
    public int RandomVelocityRange { get; set; } = 10;

    /// <summary>
    /// Random timing variation range in beats (+/-).
    /// </summary>
    public double RandomTimingRange { get; set; } = 0.02;

    /// <summary>
    /// Random length variation range in beats (+/-).
    /// </summary>
    public double RandomLengthRange { get; set; }

    /// <summary>
    /// Creates a clone of these settings.
    /// </summary>
    public MIDITransformSettings Clone()
    {
        return new MIDITransformSettings
        {
            TransposeSemitones = TransposeSemitones,
            VelocityScale = VelocityScale,
            VelocityOffset = VelocityOffset,
            ClampVelocity = ClampVelocity,
            LengthScale = LengthScale,
            PreserveLegato = PreserveLegato,
            TimeShiftBeats = TimeShiftBeats,
            RandomizeEnabled = RandomizeEnabled,
            RandomVelocityRange = RandomVelocityRange,
            RandomTimingRange = RandomTimingRange,
            RandomLengthRange = RandomLengthRange
        };
    }
}

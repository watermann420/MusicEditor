// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Distortion effect control with Drive, Tone, Mix, and Type parameters.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Distortion effect control with Drive (0-100%), Tone (0-100%), Mix (0-100%),
/// and Type (Soft/Hard/Tube/Fuzz) parameters.
/// </summary>
public partial class DistortionControl : UserControl
{
    private bool _isUpdating;

    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<DistortionParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Gets or sets the distortion type (0=Soft, 1=Hard, 2=Tube, 3=Fuzz).
    /// </summary>
    public int DistortionType
    {
        get => DistortionTypeCombo.SelectedIndex;
        set => DistortionTypeCombo.SelectedIndex = Math.Clamp(value, 0, 3);
    }

    /// <summary>
    /// Gets the distortion type as a string.
    /// </summary>
    public string DistortionTypeName => DistortionTypeCombo.SelectedItem is ComboBoxItem item
        ? item.Content?.ToString() ?? "Soft"
        : "Soft";

    /// <summary>
    /// Gets or sets the drive amount (0-100%).
    /// </summary>
    public double Drive
    {
        get => DriveSlider.Value;
        set => DriveSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the tone amount (0-100%).
    /// </summary>
    public double Tone
    {
        get => ToneSlider.Value;
        set => ToneSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the mix amount (0-100%).
    /// </summary>
    public double Mix
    {
        get => MixSlider.Value;
        set => MixSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Creates a new DistortionControl.
    /// </summary>
    public DistortionControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateValueDisplays()
    {
        DriveValue.Text = $"{DriveSlider.Value:F0}%";
        ToneValue.Text = $"{ToneSlider.Value:F0}%";
        MixValue.Text = $"{MixSlider.Value:F0}%";
    }

    private void DistortionTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;

        RaiseParameterChanged("DistortionType", DistortionTypeCombo.SelectedIndex);
    }

    private void DriveSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DriveValue == null || _isUpdating) return;

        DriveValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Drive", e.NewValue);
    }

    private void ToneSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ToneValue == null || _isUpdating) return;

        ToneValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Tone", e.NewValue);
    }

    private void MixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MixValue == null || _isUpdating) return;

        MixValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Mix", e.NewValue);
    }

    private void RaiseParameterChanged(string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new DistortionParameterChangedEventArgs(parameterName, value));
    }

    /// <summary>
    /// Sets all parameters at once without triggering individual change events.
    /// </summary>
    public void SetParameters(int distortionType, double drive, double tone, double mix)
    {
        _isUpdating = true;
        try
        {
            DistortionTypeCombo.SelectedIndex = Math.Clamp(distortionType, 0, 3);
            DriveSlider.Value = Math.Clamp(drive, 0, 100);
            ToneSlider.Value = Math.Clamp(tone, 0, 100);
            MixSlider.Value = Math.Clamp(mix, 0, 100);
            UpdateValueDisplays();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Resets all parameters to defaults.
    /// </summary>
    public void Reset()
    {
        SetParameters(0, 50, 50, 100);
    }
}

/// <summary>
/// Event arguments for distortion parameter changes.
/// </summary>
public class DistortionParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name (DistortionType, Drive, Tone, Mix).
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public DistortionParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

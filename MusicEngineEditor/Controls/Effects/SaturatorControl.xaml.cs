// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Saturator effect control with drive, saturation type, tone, output level, and mix.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Saturator effect control with drive amount, saturation type selector (tube, tape, transistor, digital),
/// tone slider, output level, and mix slider.
/// </summary>
public partial class SaturatorControl : UserControl
{
    private bool _isUpdating;

    private static readonly string[] TypeDescriptions =
    {
        "Warm, even harmonics, soft clipping",
        "Compression, HF rolloff, subtle distortion",
        "Bright, odd harmonics, aggressive",
        "Clean, precise, hard clipping"
    };

    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<SaturatorParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Gets or sets the saturation type (0=Tube, 1=Tape, 2=Transistor, 3=Digital).
    /// </summary>
    public int SaturationType
    {
        get => TypeCombo.SelectedIndex;
        set => TypeCombo.SelectedIndex = Math.Clamp(value, 0, 3);
    }

    /// <summary>
    /// Gets the saturation type as a string.
    /// </summary>
    public string SaturationTypeName => TypeCombo.SelectedItem is ComboBoxItem item
        ? item.Content?.ToString() ?? "Tube"
        : "Tube";

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
    /// Gets or sets the output level in dB (-24 to +12).
    /// </summary>
    public double OutputLevel
    {
        get => OutputSlider.Value;
        set => OutputSlider.Value = Math.Clamp(value, -24, 12);
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
    /// Creates a new SaturatorControl.
    /// </summary>
    public SaturatorControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateValueDisplays()
    {
        DriveValue.Text = $"{DriveSlider.Value:F0}%";
        ToneValue.Text = GetToneDisplayText(ToneSlider.Value);
        OutputValue.Text = $"{OutputSlider.Value:+0.0;-0.0;0.0} dB";
        MixValue.Text = $"{MixSlider.Value:F0}%";
        UpdateTypeDescription();
    }

    private static string GetToneDisplayText(double value)
    {
        if (value < 40)
            return $"Dark ({value:F0}%)";
        if (value > 60)
            return $"Bright ({value:F0}%)";
        return $"Neutral ({value:F0}%)";
    }

    private void UpdateTypeDescription()
    {
        if (TypeCombo.SelectedIndex >= 0 && TypeCombo.SelectedIndex < TypeDescriptions.Length)
        {
            TypeDescription.Text = TypeDescriptions[TypeCombo.SelectedIndex];
        }
    }

    private void TypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;

        UpdateTypeDescription();
        RaiseParameterChanged("Type", TypeCombo.SelectedIndex);
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

        ToneValue.Text = GetToneDisplayText(e.NewValue);
        RaiseParameterChanged("Tone", e.NewValue);
    }

    private void OutputSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (OutputValue == null || _isUpdating) return;

        OutputValue.Text = $"{e.NewValue:+0.0;-0.0;0.0} dB";
        RaiseParameterChanged("Output", e.NewValue);
    }

    private void MixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MixValue == null || _isUpdating) return;

        MixValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Mix", e.NewValue);
    }

    private void RaiseParameterChanged(string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new SaturatorParameterChangedEventArgs(parameterName, value));
    }

    /// <summary>
    /// Sets all parameters at once without triggering individual change events.
    /// </summary>
    public void SetParameters(int saturationType, double drive, double tone, double output, double mix)
    {
        _isUpdating = true;
        try
        {
            TypeCombo.SelectedIndex = Math.Clamp(saturationType, 0, 3);
            DriveSlider.Value = Math.Clamp(drive, 0, 100);
            ToneSlider.Value = Math.Clamp(tone, 0, 100);
            OutputSlider.Value = Math.Clamp(output, -24, 12);
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
        SetParameters(0, 30, 50, 0, 100);
    }
}

/// <summary>
/// Event arguments for saturator parameter changes.
/// </summary>
public class SaturatorParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name (Type, Drive, Tone, Output, Mix).
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public SaturatorParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

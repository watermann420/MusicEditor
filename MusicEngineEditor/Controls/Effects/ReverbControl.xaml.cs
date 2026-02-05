// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Reverb effect control with Room Size, Damping, Width, Pre-Delay, and Mix parameters.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Reverb effect control with parameters for Room Size, Damping, Width, Pre-Delay, and Mix.
/// </summary>
public partial class ReverbControl : UserControl
{
    /// <summary>
    /// Event raised when a parameter value changes.
    /// </summary>
    public event EventHandler<ReverbParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Gets or sets the room size (0-1).
    /// </summary>
    public double RoomSize
    {
        get => RoomSizeSlider.Value;
        set => RoomSizeSlider.Value = Math.Clamp(value, 0, 1);
    }

    /// <summary>
    /// Gets or sets the damping (0-1).
    /// </summary>
    public double Damping
    {
        get => DampingSlider.Value;
        set => DampingSlider.Value = Math.Clamp(value, 0, 1);
    }

    /// <summary>
    /// Gets or sets the stereo width (0-1).
    /// </summary>
    public new double Width
    {
        get => WidthSlider.Value;
        set => WidthSlider.Value = Math.Clamp(value, 0, 1);
    }

    /// <summary>
    /// Gets or sets the pre-delay in milliseconds (0-100).
    /// </summary>
    public double PreDelay
    {
        get => PreDelaySlider.Value;
        set => PreDelaySlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the dry/wet mix percentage (0-100).
    /// </summary>
    public double Mix
    {
        get => MixSlider.Value;
        set => MixSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Creates a new ReverbControl.
    /// </summary>
    public ReverbControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateValueDisplays()
    {
        if (RoomSizeValue != null) RoomSizeValue.Text = $"{RoomSizeSlider.Value:F2}";
        if (DampingValue != null) DampingValue.Text = $"{DampingSlider.Value:F2}";
        if (WidthValue != null) WidthValue.Text = $"{WidthSlider.Value:F2}";
        if (PreDelayValue != null) PreDelayValue.Text = $"{PreDelaySlider.Value:F0} ms";
        if (MixValue != null) MixValue.Text = $"{MixSlider.Value:F0}%";
    }

    private void RoomSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RoomSizeValue == null) return;
        RoomSizeValue.Text = $"{e.NewValue:F2}";
        ParameterChanged?.Invoke(this, new ReverbParameterChangedEventArgs("RoomSize", e.NewValue));
    }

    private void DampingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DampingValue == null) return;
        DampingValue.Text = $"{e.NewValue:F2}";
        ParameterChanged?.Invoke(this, new ReverbParameterChangedEventArgs("Damping", e.NewValue));
    }

    private void WidthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WidthValue == null) return;
        WidthValue.Text = $"{e.NewValue:F2}";
        ParameterChanged?.Invoke(this, new ReverbParameterChangedEventArgs("Width", e.NewValue));
    }

    private void PreDelaySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PreDelayValue == null) return;
        PreDelayValue.Text = $"{e.NewValue:F0} ms";
        ParameterChanged?.Invoke(this, new ReverbParameterChangedEventArgs("PreDelay", e.NewValue));
    }

    private void MixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MixValue == null) return;
        MixValue.Text = $"{e.NewValue:F0}%";
        ParameterChanged?.Invoke(this, new ReverbParameterChangedEventArgs("Mix", e.NewValue));
    }

    /// <summary>
    /// Resets all parameters to defaults.
    /// </summary>
    public void Reset()
    {
        RoomSizeSlider.Value = 0.5;
        DampingSlider.Value = 0.5;
        WidthSlider.Value = 1.0;
        PreDelaySlider.Value = 20;
        MixSlider.Value = 30;
    }
}

/// <summary>
/// Event arguments for reverb parameter changes.
/// </summary>
public class ReverbParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public ReverbParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Chorus effect control with Rate, Depth, Mix, and Voices parameters.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Chorus effect control with parameters for Rate, Depth, Mix, and Voices.
/// </summary>
public partial class ChorusControl : UserControl
{
    /// <summary>
    /// Event raised when a parameter value changes.
    /// </summary>
    public event EventHandler<ChorusParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Gets or sets the LFO rate in Hz (0.1-10).
    /// </summary>
    public double Rate
    {
        get => RateSlider.Value;
        set => RateSlider.Value = Math.Clamp(value, 0.1, 10);
    }

    /// <summary>
    /// Gets or sets the modulation depth percentage (0-100).
    /// </summary>
    public double Depth
    {
        get => DepthSlider.Value;
        set => DepthSlider.Value = Math.Clamp(value, 0, 100);
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
    /// Gets or sets the number of chorus voices (1-4).
    /// </summary>
    public int Voices
    {
        get => (int)VoicesSlider.Value;
        set => VoicesSlider.Value = Math.Clamp(value, 1, 4);
    }

    /// <summary>
    /// Creates a new ChorusControl.
    /// </summary>
    public ChorusControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateValueDisplays()
    {
        if (RateValue != null) RateValue.Text = $"{RateSlider.Value:F2} Hz";
        if (DepthValue != null) DepthValue.Text = $"{DepthSlider.Value:F0}%";
        if (MixValue != null) MixValue.Text = $"{MixSlider.Value:F0}%";
        if (VoicesValue != null) VoicesValue.Text = $"{(int)VoicesSlider.Value}";
    }

    private void RateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (RateValue == null) return;
        RateValue.Text = $"{e.NewValue:F2} Hz";
        ParameterChanged?.Invoke(this, new ChorusParameterChangedEventArgs("Rate", e.NewValue));
    }

    private void DepthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DepthValue == null) return;
        DepthValue.Text = $"{e.NewValue:F0}%";
        ParameterChanged?.Invoke(this, new ChorusParameterChangedEventArgs("Depth", e.NewValue));
    }

    private void MixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MixValue == null) return;
        MixValue.Text = $"{e.NewValue:F0}%";
        ParameterChanged?.Invoke(this, new ChorusParameterChangedEventArgs("Mix", e.NewValue));
    }

    private void VoicesSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (VoicesValue == null) return;
        int voices = (int)e.NewValue;
        VoicesValue.Text = $"{voices}";
        ParameterChanged?.Invoke(this, new ChorusParameterChangedEventArgs("Voices", voices));
    }

    /// <summary>
    /// Resets all parameters to defaults.
    /// </summary>
    public void Reset()
    {
        RateSlider.Value = 1.0;
        DepthSlider.Value = 50;
        MixSlider.Value = 50;
        VoicesSlider.Value = 2;
    }
}

/// <summary>
/// Event arguments for chorus parameter changes.
/// </summary>
public class ChorusParameterChangedEventArgs : EventArgs
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
    public ChorusParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Tape Saturation effect control with input gain, saturation, bias, HF rolloff, wow/flutter, noise floor, and hiss.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Tape Saturation effect control with input gain, saturation amount, tape speed selector,
/// bias, high frequency rolloff, wow/flutter, noise floor, and hiss amount.
/// </summary>
public partial class TapeSaturationControl : UserControl
{
    private bool _isUpdating;

    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<TapeSaturationParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Gets or sets the input gain in dB (-24 to +24).
    /// </summary>
    public double InputGain
    {
        get => InputGainSlider.Value;
        set => InputGainSlider.Value = Math.Clamp(value, -24, 24);
    }

    /// <summary>
    /// Gets or sets the saturation amount (0-100%).
    /// </summary>
    public double SaturationAmount
    {
        get => SaturationSlider.Value;
        set => SaturationSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the tape speed (0=7.5 IPS, 1=15 IPS, 2=30 IPS).
    /// </summary>
    public int TapeSpeed
    {
        get
        {
            if (Speed7_5Radio.IsChecked == true) return 0;
            if (Speed15Radio.IsChecked == true) return 1;
            return 2;
        }
        set
        {
            Speed7_5Radio.IsChecked = value == 0;
            Speed15Radio.IsChecked = value == 1;
            Speed30Radio.IsChecked = value == 2;
        }
    }

    /// <summary>
    /// Gets the tape speed as IPS value.
    /// </summary>
    public double TapeSpeedIPS => TapeSpeed switch
    {
        0 => 7.5,
        1 => 15.0,
        2 => 30.0,
        _ => 15.0
    };

    /// <summary>
    /// Gets or sets the bias amount (0-100%).
    /// </summary>
    public double Bias
    {
        get => BiasSlider.Value;
        set => BiasSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the high frequency rolloff amount (0-100%).
    /// </summary>
    public double HighFrequencyRolloff
    {
        get => HFRolloffSlider.Value;
        set => HFRolloffSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the wow/flutter amount (0-100%).
    /// </summary>
    public double WowFlutter
    {
        get => WowFlutterSlider.Value;
        set => WowFlutterSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the noise floor in dB (-96 to -30).
    /// </summary>
    public double NoiseFloor
    {
        get => NoiseFloorSlider.Value;
        set => NoiseFloorSlider.Value = Math.Clamp(value, -96, -30);
    }

    /// <summary>
    /// Gets or sets the hiss amount (0-100%).
    /// </summary>
    public double HissAmount
    {
        get => HissSlider.Value;
        set => HissSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Creates a new TapeSaturationControl.
    /// </summary>
    public TapeSaturationControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateValueDisplays()
    {
        InputGainValue.Text = $"{InputGainSlider.Value:+0.0;-0.0;0.0} dB";
        SaturationValue.Text = $"{SaturationSlider.Value:F0}%";
        BiasValue.Text = $"{BiasSlider.Value:F0}%";
        HFRolloffValue.Text = $"{HFRolloffSlider.Value:F0}%";
        WowFlutterValue.Text = $"{WowFlutterSlider.Value:F0}%";
        NoiseFloorValue.Text = $"{NoiseFloorSlider.Value:F0} dB";
        HissValue.Text = $"{HissSlider.Value:F0}%";
    }

    private void SpeedRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;

        RaiseParameterChanged("TapeSpeed", TapeSpeedIPS);
    }

    private void InputGainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (InputGainValue == null || _isUpdating) return;

        InputGainValue.Text = $"{e.NewValue:+0.0;-0.0;0.0} dB";
        RaiseParameterChanged("InputGain", e.NewValue);
    }

    private void SaturationSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SaturationValue == null || _isUpdating) return;

        SaturationValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Saturation", e.NewValue);
    }

    private void BiasSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BiasValue == null || _isUpdating) return;

        BiasValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Bias", e.NewValue);
    }

    private void HFRolloffSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HFRolloffValue == null || _isUpdating) return;

        HFRolloffValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("HFRolloff", e.NewValue);
    }

    private void WowFlutterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WowFlutterValue == null || _isUpdating) return;

        WowFlutterValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("WowFlutter", e.NewValue);
    }

    private void NoiseFloorSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (NoiseFloorValue == null || _isUpdating) return;

        NoiseFloorValue.Text = $"{e.NewValue:F0} dB";
        RaiseParameterChanged("NoiseFloor", e.NewValue);
    }

    private void HissSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HissValue == null || _isUpdating) return;

        HissValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Hiss", e.NewValue);
    }

    private void RaiseParameterChanged(string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new TapeSaturationParameterChangedEventArgs(parameterName, value));
    }

    /// <summary>
    /// Sets all parameters at once without triggering individual change events.
    /// </summary>
    public void SetParameters(double inputGain, double saturation, int tapeSpeed, double bias,
        double hfRolloff, double wowFlutter, double noiseFloor, double hiss)
    {
        _isUpdating = true;
        try
        {
            InputGainSlider.Value = Math.Clamp(inputGain, -24, 24);
            SaturationSlider.Value = Math.Clamp(saturation, 0, 100);
            TapeSpeed = Math.Clamp(tapeSpeed, 0, 2);
            BiasSlider.Value = Math.Clamp(bias, 0, 100);
            HFRolloffSlider.Value = Math.Clamp(hfRolloff, 0, 100);
            WowFlutterSlider.Value = Math.Clamp(wowFlutter, 0, 100);
            NoiseFloorSlider.Value = Math.Clamp(noiseFloor, -96, -30);
            HissSlider.Value = Math.Clamp(hiss, 0, 100);
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
        SetParameters(0, 40, 1, 50, 30, 15, -70, 20);
    }
}

/// <summary>
/// Event arguments for tape saturation parameter changes.
/// </summary>
public class TapeSaturationParameterChangedEventArgs : EventArgs
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
    public TapeSaturationParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

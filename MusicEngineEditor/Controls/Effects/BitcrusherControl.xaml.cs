// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Bitcrusher effect control with bit depth, sample rate reduction, dither, jitter, and mix.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Bitcrusher effect control with bit depth (1-24), sample rate reduction,
/// dither toggle, jitter amount, and mix slider.
/// </summary>
public partial class BitcrusherControl : UserControl
{
    private bool _isUpdating;

    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<BitcrusherParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Gets or sets the bit depth (1-24).
    /// </summary>
    public int BitDepth
    {
        get => (int)BitDepthSlider.Value;
        set => BitDepthSlider.Value = Math.Clamp(value, 1, 24);
    }

    /// <summary>
    /// Gets or sets the sample rate reduction amount (0-100%).
    /// </summary>
    public double SampleRateReduction
    {
        get => SampleRateSlider.Value;
        set => SampleRateSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets whether dither is enabled.
    /// </summary>
    public bool DitherEnabled
    {
        get => DitherToggle.IsChecked == true;
        set => DitherToggle.IsChecked = value;
    }

    /// <summary>
    /// Gets or sets the jitter amount (0-100%).
    /// </summary>
    public double JitterAmount
    {
        get => JitterSlider.Value;
        set => JitterSlider.Value = Math.Clamp(value, 0, 100);
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
    /// Creates a new BitcrusherControl.
    /// </summary>
    public BitcrusherControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateValueDisplays()
    {
        BitDepthValue.Text = $"{(int)BitDepthSlider.Value} bit";
        UpdateSampleRateDisplay();
        JitterValue.Text = $"{JitterSlider.Value:F0}%";
        MixValue.Text = $"{MixSlider.Value:F0}%";
        DitherToggle.Content = DitherToggle.IsChecked == true ? "ON" : "OFF";
    }

    private void UpdateSampleRateDisplay()
    {
        if (SampleRateSlider.Value <= 0)
        {
            SampleRateValue.Text = "Off";
        }
        else
        {
            // Calculate effective sample rate (assuming 44100 base)
            double factor = 1.0 + (SampleRateSlider.Value / 10.0);
            double effectiveRate = 44100.0 / factor;

            if (effectiveRate >= 1000)
                SampleRateValue.Text = $"{effectiveRate / 1000:F1} kHz";
            else
                SampleRateValue.Text = $"{effectiveRate:F0} Hz";
        }
    }

    private void BitDepthSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BitDepthValue == null || _isUpdating) return;

        BitDepthValue.Text = $"{(int)e.NewValue} bit";
        RaiseParameterChanged("BitDepth", (int)e.NewValue);
    }

    private void SampleRateSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SampleRateValue == null || _isUpdating) return;

        UpdateSampleRateDisplay();
        RaiseParameterChanged("SampleRate", e.NewValue);
    }

    private void JitterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (JitterValue == null || _isUpdating) return;

        JitterValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Jitter", e.NewValue);
    }

    private void DitherToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;

        DitherToggle.Content = DitherToggle.IsChecked == true ? "ON" : "OFF";
        RaiseParameterChanged("Dither", DitherToggle.IsChecked == true ? 1 : 0);
    }

    private void MixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MixValue == null || _isUpdating) return;

        MixValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Mix", e.NewValue);
    }

    private void BitPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string tagStr && int.TryParse(tagStr, out int bits))
        {
            BitDepthSlider.Value = bits;
        }
    }

    private void RaiseParameterChanged(string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new BitcrusherParameterChangedEventArgs(parameterName, value));
    }

    /// <summary>
    /// Sets all parameters at once without triggering individual change events.
    /// </summary>
    public void SetParameters(int bitDepth, double sampleRate, bool dither, double jitter, double mix)
    {
        _isUpdating = true;
        try
        {
            BitDepthSlider.Value = Math.Clamp(bitDepth, 1, 24);
            SampleRateSlider.Value = Math.Clamp(sampleRate, 0, 100);
            DitherToggle.IsChecked = dither;
            JitterSlider.Value = Math.Clamp(jitter, 0, 100);
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
        SetParameters(16, 0, false, 0, 100);
    }
}

/// <summary>
/// Event arguments for bitcrusher parameter changes.
/// </summary>
public class BitcrusherParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name (BitDepth, SampleRate, Dither, Jitter, Mix).
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public BitcrusherParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

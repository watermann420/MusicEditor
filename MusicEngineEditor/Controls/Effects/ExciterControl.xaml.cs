// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Exciter effect control with frequency range, harmonic amount, brightness, and mix.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Exciter effect control with frequency range (low/high bands),
/// harmonic amount slider, brightness slider, and mix slider.
/// </summary>
public partial class ExciterControl : UserControl
{
    private bool _isUpdating;

    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<ExciterParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Gets or sets the low frequency bound in Hz (20-2000).
    /// </summary>
    public double LowFrequency
    {
        get => LowFreqSlider.Value;
        set => LowFreqSlider.Value = Math.Clamp(value, 20, 2000);
    }

    /// <summary>
    /// Gets or sets the high frequency bound in Hz (1000-20000).
    /// </summary>
    public double HighFrequency
    {
        get => HighFreqSlider.Value;
        set => HighFreqSlider.Value = Math.Clamp(value, 1000, 20000);
    }

    /// <summary>
    /// Gets or sets the harmonic amount (0-100%).
    /// </summary>
    public double HarmonicAmount
    {
        get => HarmonicSlider.Value;
        set => HarmonicSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the brightness amount (0-100%).
    /// </summary>
    public double Brightness
    {
        get => BrightnessSlider.Value;
        set => BrightnessSlider.Value = Math.Clamp(value, 0, 100);
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
    /// Creates a new ExciterControl.
    /// </summary>
    public ExciterControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateValueDisplays()
    {
        LowFreqValue.Text = FormatFrequency(LowFreqSlider.Value);
        HighFreqValue.Text = FormatFrequency(HighFreqSlider.Value);
        HarmonicValue.Text = $"{HarmonicSlider.Value:F0}%";
        BrightnessValue.Text = GetBrightnessDisplayText(BrightnessSlider.Value);
        MixValue.Text = $"{MixSlider.Value:F0}%";
    }

    private static string FormatFrequency(double hz)
    {
        if (hz >= 1000)
            return $"{hz / 1000:F1} kHz";
        return $"{hz:F0} Hz";
    }

    private static string GetBrightnessDisplayText(double value)
    {
        if (value < 35)
            return $"Warm ({value:F0}%)";
        if (value > 65)
            return $"Air ({value:F0}%)";
        return $"Neutral ({value:F0}%)";
    }

    private void LowFreqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (LowFreqValue == null || _isUpdating) return;

        // Ensure low frequency doesn't exceed high frequency
        if (e.NewValue >= HighFreqSlider.Value - 100)
        {
            LowFreqSlider.Value = HighFreqSlider.Value - 100;
            return;
        }

        LowFreqValue.Text = FormatFrequency(e.NewValue);
        RaiseParameterChanged("LowFrequency", e.NewValue);
    }

    private void HighFreqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HighFreqValue == null || _isUpdating) return;

        // Ensure high frequency doesn't go below low frequency
        if (e.NewValue <= LowFreqSlider.Value + 100)
        {
            HighFreqSlider.Value = LowFreqSlider.Value + 100;
            return;
        }

        HighFreqValue.Text = FormatFrequency(e.NewValue);
        RaiseParameterChanged("HighFrequency", e.NewValue);
    }

    private void HarmonicSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (HarmonicValue == null || _isUpdating) return;

        HarmonicValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Harmonics", e.NewValue);
    }

    private void BrightnessSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (BrightnessValue == null || _isUpdating) return;

        BrightnessValue.Text = GetBrightnessDisplayText(e.NewValue);
        RaiseParameterChanged("Brightness", e.NewValue);
    }

    private void MixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MixValue == null || _isUpdating) return;

        MixValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Mix", e.NewValue);
    }

    private void FreqPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string preset)
        {
            _isUpdating = true;
            try
            {
                switch (preset.ToLowerInvariant())
                {
                    case "bass":
                        LowFreqSlider.Value = 40;
                        HighFreqSlider.Value = 500;
                        break;
                    case "mid":
                        LowFreqSlider.Value = 500;
                        HighFreqSlider.Value = 4000;
                        break;
                    case "air":
                        LowFreqSlider.Value = 4000;
                        HighFreqSlider.Value = 16000;
                        break;
                    case "full":
                        LowFreqSlider.Value = 100;
                        HighFreqSlider.Value = 12000;
                        break;
                }
                UpdateValueDisplays();
            }
            finally
            {
                _isUpdating = false;
            }

            RaiseParameterChanged("LowFrequency", LowFreqSlider.Value);
            RaiseParameterChanged("HighFrequency", HighFreqSlider.Value);
        }
    }

    private void RaiseParameterChanged(string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new ExciterParameterChangedEventArgs(parameterName, value));
    }

    /// <summary>
    /// Sets all parameters at once without triggering individual change events.
    /// </summary>
    public void SetParameters(double lowFreq, double highFreq, double harmonics, double brightness, double mix)
    {
        _isUpdating = true;
        try
        {
            LowFreqSlider.Value = Math.Clamp(lowFreq, 20, 2000);
            HighFreqSlider.Value = Math.Clamp(highFreq, 1000, 20000);

            // Ensure valid range
            if (LowFreqSlider.Value >= HighFreqSlider.Value)
            {
                HighFreqSlider.Value = LowFreqSlider.Value + 100;
            }

            HarmonicSlider.Value = Math.Clamp(harmonics, 0, 100);
            BrightnessSlider.Value = Math.Clamp(brightness, 0, 100);
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
        SetParameters(200, 8000, 40, 50, 100);
    }
}

/// <summary>
/// Event arguments for exciter parameter changes.
/// </summary>
public class ExciterParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name (LowFrequency, HighFrequency, Harmonics, Brightness, Mix).
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public ExciterParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

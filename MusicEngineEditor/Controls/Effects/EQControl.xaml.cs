// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: 4-Band Parametric EQ control with Frequency, Gain, and Q for each band.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// 4-Band Parametric EQ control.
/// Each band has Frequency (20-20000Hz logarithmic), Gain (-12 to +12 dB), and Q (0.1-10) parameters.
/// </summary>
public partial class EQControl : UserControl
{
    private bool _isUpdating;

    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<EQParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Creates a new EQControl.
    /// </summary>
    public EQControl()
    {
        InitializeComponent();
        UpdateAllValueDisplays();
    }

    #region Band Parameter Properties

    /// <summary>
    /// Gets or sets Band 1 frequency in Hz.
    /// </summary>
    public double Band1Frequency
    {
        get => SliderToFrequency(Band1FreqSlider.Value);
        set => Band1FreqSlider.Value = FrequencyToSlider(value);
    }

    /// <summary>
    /// Gets or sets Band 1 gain in dB (-12 to +12).
    /// </summary>
    public double Band1Gain
    {
        get => SliderToGain(Band1GainSlider.Value);
        set => Band1GainSlider.Value = GainToSlider(value);
    }

    /// <summary>
    /// Gets or sets Band 1 Q (0.1-10).
    /// </summary>
    public double Band1Q
    {
        get => SliderToQ(Band1QSlider.Value);
        set => Band1QSlider.Value = QToSlider(value);
    }

    /// <summary>
    /// Gets or sets Band 2 frequency in Hz.
    /// </summary>
    public double Band2Frequency
    {
        get => SliderToFrequency(Band2FreqSlider.Value);
        set => Band2FreqSlider.Value = FrequencyToSlider(value);
    }

    /// <summary>
    /// Gets or sets Band 2 gain in dB (-12 to +12).
    /// </summary>
    public double Band2Gain
    {
        get => SliderToGain(Band2GainSlider.Value);
        set => Band2GainSlider.Value = GainToSlider(value);
    }

    /// <summary>
    /// Gets or sets Band 2 Q (0.1-10).
    /// </summary>
    public double Band2Q
    {
        get => SliderToQ(Band2QSlider.Value);
        set => Band2QSlider.Value = QToSlider(value);
    }

    /// <summary>
    /// Gets or sets Band 3 frequency in Hz.
    /// </summary>
    public double Band3Frequency
    {
        get => SliderToFrequency(Band3FreqSlider.Value);
        set => Band3FreqSlider.Value = FrequencyToSlider(value);
    }

    /// <summary>
    /// Gets or sets Band 3 gain in dB (-12 to +12).
    /// </summary>
    public double Band3Gain
    {
        get => SliderToGain(Band3GainSlider.Value);
        set => Band3GainSlider.Value = GainToSlider(value);
    }

    /// <summary>
    /// Gets or sets Band 3 Q (0.1-10).
    /// </summary>
    public double Band3Q
    {
        get => SliderToQ(Band3QSlider.Value);
        set => Band3QSlider.Value = QToSlider(value);
    }

    /// <summary>
    /// Gets or sets Band 4 frequency in Hz.
    /// </summary>
    public double Band4Frequency
    {
        get => SliderToFrequency(Band4FreqSlider.Value);
        set => Band4FreqSlider.Value = FrequencyToSlider(value);
    }

    /// <summary>
    /// Gets or sets Band 4 gain in dB (-12 to +12).
    /// </summary>
    public double Band4Gain
    {
        get => SliderToGain(Band4GainSlider.Value);
        set => Band4GainSlider.Value = GainToSlider(value);
    }

    /// <summary>
    /// Gets or sets Band 4 Q (0.1-10).
    /// </summary>
    public double Band4Q
    {
        get => SliderToQ(Band4QSlider.Value);
        set => Band4QSlider.Value = QToSlider(value);
    }

    #endregion

    #region Value Conversions

    // Frequency: 0-100 slider -> 20-20000 Hz (logarithmic)
    private static double SliderToFrequency(double slider)
    {
        double minLog = Math.Log10(20);
        double maxLog = Math.Log10(20000);
        double logValue = minLog + (slider / 100.0) * (maxLog - minLog);
        return Math.Pow(10, logValue);
    }

    private static double FrequencyToSlider(double hz)
    {
        double minLog = Math.Log10(20);
        double maxLog = Math.Log10(20000);
        double logValue = Math.Log10(Math.Clamp(hz, 20, 20000));
        return ((logValue - minLog) / (maxLog - minLog)) * 100;
    }

    // Gain: 0-100 slider -> -12 to +12 dB (linear)
    private static double SliderToGain(double slider)
    {
        return (slider / 100.0) * 24 - 12;
    }

    private static double GainToSlider(double gain)
    {
        return ((Math.Clamp(gain, -12, 12) + 12) / 24.0) * 100;
    }

    // Q: 0-100 slider -> 0.1-10 (logarithmic)
    private static double SliderToQ(double slider)
    {
        double minLog = Math.Log10(0.1);
        double maxLog = Math.Log10(10);
        double logValue = minLog + (slider / 100.0) * (maxLog - minLog);
        return Math.Pow(10, logValue);
    }

    private static double QToSlider(double q)
    {
        double minLog = Math.Log10(0.1);
        double maxLog = Math.Log10(10);
        double logValue = Math.Log10(Math.Clamp(q, 0.1, 10));
        return ((logValue - minLog) / (maxLog - minLog)) * 100;
    }

    private static string FormatFrequency(double hz)
    {
        if (hz >= 1000)
            return $"{hz / 1000:F1} kHz";
        return $"{hz:F0} Hz";
    }

    #endregion

    #region Value Display Updates

    private void UpdateAllValueDisplays()
    {
        UpdateBand1Values();
        UpdateBand2Values();
        UpdateBand3Values();
        UpdateBand4Values();
    }

    private void UpdateBand1Values()
    {
        Band1FreqValue.Text = FormatFrequency(SliderToFrequency(Band1FreqSlider.Value));
        Band1GainValue.Text = $"{SliderToGain(Band1GainSlider.Value):+0.0;-0.0;0.0} dB";
        Band1QValue.Text = $"{SliderToQ(Band1QSlider.Value):F2}";
    }

    private void UpdateBand2Values()
    {
        Band2FreqValue.Text = FormatFrequency(SliderToFrequency(Band2FreqSlider.Value));
        Band2GainValue.Text = $"{SliderToGain(Band2GainSlider.Value):+0.0;-0.0;0.0} dB";
        Band2QValue.Text = $"{SliderToQ(Band2QSlider.Value):F2}";
    }

    private void UpdateBand3Values()
    {
        Band3FreqValue.Text = FormatFrequency(SliderToFrequency(Band3FreqSlider.Value));
        Band3GainValue.Text = $"{SliderToGain(Band3GainSlider.Value):+0.0;-0.0;0.0} dB";
        Band3QValue.Text = $"{SliderToQ(Band3QSlider.Value):F2}";
    }

    private void UpdateBand4Values()
    {
        Band4FreqValue.Text = FormatFrequency(SliderToFrequency(Band4FreqSlider.Value));
        Band4GainValue.Text = $"{SliderToGain(Band4GainSlider.Value):+0.0;-0.0;0.0} dB";
        Band4QValue.Text = $"{SliderToQ(Band4QSlider.Value):F2}";
    }

    #endregion

    #region Event Handlers

    private void Band1FreqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Band1FreqValue == null || _isUpdating) return;
        UpdateBand1Values();
        RaiseParameterChanged(1, "Frequency", SliderToFrequency(e.NewValue));
    }

    private void Band1GainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Band1GainValue == null || _isUpdating) return;
        UpdateBand1Values();
        RaiseParameterChanged(1, "Gain", SliderToGain(e.NewValue));
    }

    private void Band1QSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Band1QValue == null || _isUpdating) return;
        UpdateBand1Values();
        RaiseParameterChanged(1, "Q", SliderToQ(e.NewValue));
    }

    private void Band2FreqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Band2FreqValue == null || _isUpdating) return;
        UpdateBand2Values();
        RaiseParameterChanged(2, "Frequency", SliderToFrequency(e.NewValue));
    }

    private void Band2GainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Band2GainValue == null || _isUpdating) return;
        UpdateBand2Values();
        RaiseParameterChanged(2, "Gain", SliderToGain(e.NewValue));
    }

    private void Band2QSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Band2QValue == null || _isUpdating) return;
        UpdateBand2Values();
        RaiseParameterChanged(2, "Q", SliderToQ(e.NewValue));
    }

    private void Band3FreqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Band3FreqValue == null || _isUpdating) return;
        UpdateBand3Values();
        RaiseParameterChanged(3, "Frequency", SliderToFrequency(e.NewValue));
    }

    private void Band3GainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Band3GainValue == null || _isUpdating) return;
        UpdateBand3Values();
        RaiseParameterChanged(3, "Gain", SliderToGain(e.NewValue));
    }

    private void Band3QSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Band3QValue == null || _isUpdating) return;
        UpdateBand3Values();
        RaiseParameterChanged(3, "Q", SliderToQ(e.NewValue));
    }

    private void Band4FreqSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Band4FreqValue == null || _isUpdating) return;
        UpdateBand4Values();
        RaiseParameterChanged(4, "Frequency", SliderToFrequency(e.NewValue));
    }

    private void Band4GainSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Band4GainValue == null || _isUpdating) return;
        UpdateBand4Values();
        RaiseParameterChanged(4, "Gain", SliderToGain(e.NewValue));
    }

    private void Band4QSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (Band4QValue == null || _isUpdating) return;
        UpdateBand4Values();
        RaiseParameterChanged(4, "Q", SliderToQ(e.NewValue));
    }

    #endregion

    private void RaiseParameterChanged(int band, string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new EQParameterChangedEventArgs(band, parameterName, value));
    }

    /// <summary>
    /// Sets all parameters for a specific band without triggering individual change events.
    /// </summary>
    public void SetBandParameters(int band, double frequencyHz, double gainDb, double q)
    {
        _isUpdating = true;
        try
        {
            switch (band)
            {
                case 1:
                    Band1FreqSlider.Value = FrequencyToSlider(frequencyHz);
                    Band1GainSlider.Value = GainToSlider(gainDb);
                    Band1QSlider.Value = QToSlider(q);
                    UpdateBand1Values();
                    break;
                case 2:
                    Band2FreqSlider.Value = FrequencyToSlider(frequencyHz);
                    Band2GainSlider.Value = GainToSlider(gainDb);
                    Band2QSlider.Value = QToSlider(q);
                    UpdateBand2Values();
                    break;
                case 3:
                    Band3FreqSlider.Value = FrequencyToSlider(frequencyHz);
                    Band3GainSlider.Value = GainToSlider(gainDb);
                    Band3QSlider.Value = QToSlider(q);
                    UpdateBand3Values();
                    break;
                case 4:
                    Band4FreqSlider.Value = FrequencyToSlider(frequencyHz);
                    Band4GainSlider.Value = GainToSlider(gainDb);
                    Band4QSlider.Value = QToSlider(q);
                    UpdateBand4Values();
                    break;
            }
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
        _isUpdating = true;
        try
        {
            // Band 1: 100 Hz, 0 dB, Q=1.0
            Band1FreqSlider.Value = FrequencyToSlider(100);
            Band1GainSlider.Value = GainToSlider(0);
            Band1QSlider.Value = QToSlider(1.0);

            // Band 2: 500 Hz, 0 dB, Q=1.0
            Band2FreqSlider.Value = FrequencyToSlider(500);
            Band2GainSlider.Value = GainToSlider(0);
            Band2QSlider.Value = QToSlider(1.0);

            // Band 3: 2000 Hz, 0 dB, Q=1.0
            Band3FreqSlider.Value = FrequencyToSlider(2000);
            Band3GainSlider.Value = GainToSlider(0);
            Band3QSlider.Value = QToSlider(1.0);

            // Band 4: 8000 Hz, 0 dB, Q=1.0
            Band4FreqSlider.Value = FrequencyToSlider(8000);
            Band4GainSlider.Value = GainToSlider(0);
            Band4QSlider.Value = QToSlider(1.0);

            UpdateAllValueDisplays();
        }
        finally
        {
            _isUpdating = false;
        }
    }
}

/// <summary>
/// Event arguments for EQ parameter changes.
/// </summary>
public class EQParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the band number (1-4).
    /// </summary>
    public int Band { get; }

    /// <summary>
    /// Gets the parameter name (Frequency, Gain, Q).
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public EQParameterChangedEventArgs(int band, string parameterName, double value)
    {
        Band = band;
        ParameterName = parameterName;
        Value = value;
    }
}

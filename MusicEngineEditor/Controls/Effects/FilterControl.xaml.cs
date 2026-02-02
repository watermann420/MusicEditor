// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Filter effect control with Type, Cutoff, and Resonance parameters.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Filter effect control with Type (Lowpass/Highpass/Bandpass/Notch),
/// Cutoff (20-20000Hz logarithmic), and Resonance (0-1) parameters.
/// </summary>
public partial class FilterControl : UserControl
{
    private bool _isUpdating;

    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<FilterParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Gets or sets the filter type (0=Lowpass, 1=Highpass, 2=Bandpass, 3=Notch).
    /// </summary>
    public int FilterType
    {
        get => FilterTypeCombo.SelectedIndex;
        set => FilterTypeCombo.SelectedIndex = Math.Clamp(value, 0, 3);
    }

    /// <summary>
    /// Gets the filter type as a string.
    /// </summary>
    public string FilterTypeName => FilterTypeCombo.SelectedItem is ComboBoxItem item
        ? item.Content?.ToString() ?? "Lowpass"
        : "Lowpass";

    /// <summary>
    /// Gets or sets the cutoff frequency in Hz (20-20000).
    /// </summary>
    public double CutoffHz
    {
        get => SliderToCutoff(CutoffSlider.Value);
        set => CutoffSlider.Value = CutoffToSlider(Math.Clamp(value, 20, 20000));
    }

    /// <summary>
    /// Gets or sets the resonance value (0-1).
    /// </summary>
    public double Resonance
    {
        get => ResonanceSlider.Value / 100.0;
        set => ResonanceSlider.Value = Math.Clamp(value, 0, 1) * 100;
    }

    /// <summary>
    /// Creates a new FilterControl.
    /// </summary>
    public FilterControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateValueDisplays()
    {
        CutoffValue.Text = FormatFrequency(SliderToCutoff(CutoffSlider.Value));
        ResonanceValue.Text = $"{ResonanceSlider.Value / 100.0:F2}";
    }

    private static string FormatFrequency(double hz)
    {
        if (hz >= 1000)
            return $"{hz / 1000:F1} kHz";
        return $"{hz:F0} Hz";
    }

    // Logarithmic conversion for cutoff frequency (0-100 slider -> 20-20000 Hz)
    private static double SliderToCutoff(double slider)
    {
        double minLog = Math.Log10(20);
        double maxLog = Math.Log10(20000);
        double logValue = minLog + (slider / 100.0) * (maxLog - minLog);
        return Math.Pow(10, logValue);
    }

    private static double CutoffToSlider(double hz)
    {
        double minLog = Math.Log10(20);
        double maxLog = Math.Log10(20000);
        double logValue = Math.Log10(Math.Max(20, hz));
        return ((logValue - minLog) / (maxLog - minLog)) * 100;
    }

    private void FilterTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;

        RaiseParameterChanged("FilterType", FilterTypeCombo.SelectedIndex);
    }

    private void CutoffSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CutoffValue == null || _isUpdating) return;

        double cutoffHz = SliderToCutoff(e.NewValue);
        CutoffValue.Text = FormatFrequency(cutoffHz);

        RaiseParameterChanged("Cutoff", cutoffHz);
    }

    private void ResonanceSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ResonanceValue == null || _isUpdating) return;

        double resonance = e.NewValue / 100.0;
        ResonanceValue.Text = $"{resonance:F2}";

        RaiseParameterChanged("Resonance", resonance);
    }

    private void RaiseParameterChanged(string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new FilterParameterChangedEventArgs(parameterName, value));
    }

    /// <summary>
    /// Sets all parameters at once without triggering individual change events.
    /// </summary>
    public void SetParameters(int filterType, double cutoffHz, double resonance)
    {
        _isUpdating = true;
        try
        {
            FilterTypeCombo.SelectedIndex = Math.Clamp(filterType, 0, 3);
            CutoffSlider.Value = CutoffToSlider(Math.Clamp(cutoffHz, 20, 20000));
            ResonanceSlider.Value = Math.Clamp(resonance, 0, 1) * 100;
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
        SetParameters(0, 1000, 0);
    }
}

/// <summary>
/// Event arguments for filter parameter changes.
/// </summary>
public class FilterParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name (FilterType, Cutoff, Resonance).
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public FilterParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

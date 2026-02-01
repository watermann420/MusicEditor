// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Limiter effect control with Ceiling, Release, and Lookahead parameters.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Limiter effect control with Ceiling (-12 to 0 dB), Release (10-1000ms),
/// and Lookahead toggle parameters.
/// </summary>
public partial class LimiterControl : UserControl
{
    private bool _isUpdating;

    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<LimiterParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Gets or sets the ceiling in dB (-12 to 0).
    /// </summary>
    public double CeilingDb
    {
        get => SliderToCeiling(CeilingSlider.Value);
        set => CeilingSlider.Value = CeilingToSlider(Math.Clamp(value, -12, 0));
    }

    /// <summary>
    /// Gets or sets the release time in milliseconds (10-1000).
    /// </summary>
    public double ReleaseMs
    {
        get => SliderToRelease(ReleaseSlider.Value);
        set => ReleaseSlider.Value = ReleaseToSlider(Math.Clamp(value, 10, 1000));
    }

    /// <summary>
    /// Gets or sets whether lookahead is enabled.
    /// </summary>
    public bool LookaheadEnabled
    {
        get => LookaheadCheckBox.IsChecked == true;
        set => LookaheadCheckBox.IsChecked = value;
    }

    /// <summary>
    /// Creates a new LimiterControl.
    /// </summary>
    public LimiterControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateValueDisplays()
    {
        CeilingValue.Text = $"{SliderToCeiling(CeilingSlider.Value):F1} dB";
        ReleaseValue.Text = $"{SliderToRelease(ReleaseSlider.Value):F0} ms";
    }

    // Ceiling: 0-100 slider -> -12 to 0 dB (linear)
    private static double SliderToCeiling(double slider)
    {
        return (slider / 100.0) * 12 - 12;
    }

    private static double CeilingToSlider(double db)
    {
        return ((db + 12) / 12.0) * 100;
    }

    // Release: 0-100 slider -> 10-1000 ms (logarithmic)
    private static double SliderToRelease(double slider)
    {
        double minLog = Math.Log10(10);
        double maxLog = Math.Log10(1000);
        double logValue = minLog + (slider / 100.0) * (maxLog - minLog);
        return Math.Pow(10, logValue);
    }

    private static double ReleaseToSlider(double ms)
    {
        double minLog = Math.Log10(10);
        double maxLog = Math.Log10(1000);
        double logValue = Math.Log10(Math.Max(10, ms));
        return ((logValue - minLog) / (maxLog - minLog)) * 100;
    }

    private void CeilingSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CeilingValue == null || _isUpdating) return;

        double ceilingDb = SliderToCeiling(e.NewValue);
        CeilingValue.Text = $"{ceilingDb:F1} dB";

        RaiseParameterChanged("Ceiling", ceilingDb);
    }

    private void ReleaseSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ReleaseValue == null || _isUpdating) return;

        double releaseMs = SliderToRelease(e.NewValue);
        ReleaseValue.Text = $"{releaseMs:F0} ms";

        RaiseParameterChanged("Release", releaseMs);
    }

    private void LookaheadCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;

        RaiseParameterChanged("Lookahead", LookaheadCheckBox.IsChecked == true ? 1 : 0);
    }

    private void RaiseParameterChanged(string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new LimiterParameterChangedEventArgs(parameterName, value));
    }

    /// <summary>
    /// Sets all parameters at once without triggering individual change events.
    /// </summary>
    public void SetParameters(double ceilingDb, double releaseMs, bool lookahead)
    {
        _isUpdating = true;
        try
        {
            CeilingSlider.Value = CeilingToSlider(Math.Clamp(ceilingDb, -12, 0));
            ReleaseSlider.Value = ReleaseToSlider(Math.Clamp(releaseMs, 10, 1000));
            LookaheadCheckBox.IsChecked = lookahead;
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
        SetParameters(-0.3, 100, true);
    }
}

/// <summary>
/// Event arguments for limiter parameter changes.
/// </summary>
public class LimiterParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name (Ceiling, Release, Lookahead).
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value. For Lookahead, 1 = enabled, 0 = disabled.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public LimiterParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Tape Stop effect control with stop/start time, direction toggle, and wow/flutter.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Tape Stop effect control with stop time, start time, direction toggle,
/// wow/flutter, and trigger button.
/// </summary>
public partial class TapeStopControl : UserControl
{
    private bool _isUpdating;

    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<TapeStopParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Event raised when the effect is triggered.
    /// </summary>
    public event EventHandler<TapeStopTriggeredEventArgs>? EffectTriggered;

    /// <summary>
    /// Gets or sets the stop time in milliseconds.
    /// </summary>
    public double StopTime
    {
        get => StopTimeSlider.Value;
        set => StopTimeSlider.Value = Math.Clamp(value, 10, 5000);
    }

    /// <summary>
    /// Gets or sets the start time in milliseconds.
    /// </summary>
    public double StartTime
    {
        get => StartTimeSlider.Value;
        set => StartTimeSlider.Value = Math.Clamp(value, 10, 5000);
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
    /// Gets or sets whether the direction is Stop (true) or Start (false).
    /// </summary>
    public bool IsStopDirection
    {
        get => StopToggle.IsChecked == true;
        set
        {
            StopToggle.IsChecked = value;
            StartToggle.IsChecked = !value;
        }
    }

    /// <summary>
    /// Creates a new TapeStopControl.
    /// </summary>
    public TapeStopControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateValueDisplays()
    {
        StopTimeValue.Text = FormatTime(StopTimeSlider.Value);
        StartTimeValue.Text = FormatTime(StartTimeSlider.Value);
        WowFlutterValue.Text = $"{WowFlutterSlider.Value:F0}%";
    }

    private static string FormatTime(double ms)
    {
        return ms < 1000 ? $"{ms:F0} ms" : $"{ms / 1000:F2} s";
    }

    private void StopTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (StopTimeValue == null || _isUpdating) return;

        StopTimeValue.Text = FormatTime(e.NewValue);
        RaiseParameterChanged("StopTime", e.NewValue);
    }

    private void StartTimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (StartTimeValue == null || _isUpdating) return;

        StartTimeValue.Text = FormatTime(e.NewValue);
        RaiseParameterChanged("StartTime", e.NewValue);
    }

    private void WowFlutterSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WowFlutterValue == null || _isUpdating) return;

        WowFlutterValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("WowFlutter", e.NewValue);
    }

    private void StopToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;

        StopToggle.IsChecked = true;
        StartToggle.IsChecked = false;
        RaiseParameterChanged("Direction", 0);
    }

    private void StartToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;

        StopToggle.IsChecked = false;
        StartToggle.IsChecked = true;
        RaiseParameterChanged("Direction", 1);
    }

    private void TriggerButton_Click(object sender, RoutedEventArgs e)
    {
        var direction = StopToggle.IsChecked == true ? "Stop" : "Start";
        var time = StopToggle.IsChecked == true ? StopTime : StartTime;

        EffectTriggered?.Invoke(this, new TapeStopTriggeredEventArgs(direction, time, WowFlutter));
    }

    private void RaiseParameterChanged(string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new TapeStopParameterChangedEventArgs(parameterName, value));
    }

    /// <summary>
    /// Sets all parameters at once without triggering individual change events.
    /// </summary>
    public void SetParameters(double stopTime, double startTime, double wowFlutter, bool isStopDirection)
    {
        _isUpdating = true;
        try
        {
            StopTimeSlider.Value = Math.Clamp(stopTime, 10, 5000);
            StartTimeSlider.Value = Math.Clamp(startTime, 10, 5000);
            WowFlutterSlider.Value = Math.Clamp(wowFlutter, 0, 100);
            StopToggle.IsChecked = isStopDirection;
            StartToggle.IsChecked = !isStopDirection;
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
        SetParameters(500, 300, 25, true);
    }
}

/// <summary>
/// Event arguments for tape stop parameter changes.
/// </summary>
public class TapeStopParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name (StopTime, StartTime, WowFlutter, Direction).
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public TapeStopParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

/// <summary>
/// Event arguments for when tape stop effect is triggered.
/// </summary>
public class TapeStopTriggeredEventArgs : EventArgs
{
    /// <summary>
    /// Gets the direction ("Stop" or "Start").
    /// </summary>
    public string Direction { get; }

    /// <summary>
    /// Gets the time in milliseconds.
    /// </summary>
    public double Time { get; }

    /// <summary>
    /// Gets the wow/flutter amount.
    /// </summary>
    public double WowFlutter { get; }

    /// <summary>
    /// Creates new event arguments.
    /// </summary>
    public TapeStopTriggeredEventArgs(string direction, double time, double wowFlutter)
    {
        Direction = direction;
        Time = time;
        WowFlutter = wowFlutter;
    }
}

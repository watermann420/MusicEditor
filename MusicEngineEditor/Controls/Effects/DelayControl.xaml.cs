// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Delay effect control with Time, Feedback, Mix, Sync, and Ping-Pong parameters.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Delay effect control with parameters for Time, Feedback, Mix, and toggles for Sync and Ping-Pong.
/// </summary>
public partial class DelayControl : UserControl
{
    /// <summary>
    /// Event raised when a parameter value changes.
    /// </summary>
    public event EventHandler<DelayParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Gets or sets the delay time in milliseconds (1-2000).
    /// </summary>
    public double Time
    {
        get => TimeSlider.Value;
        set => TimeSlider.Value = Math.Clamp(value, 1, 2000);
    }

    /// <summary>
    /// Gets or sets the feedback percentage (0-100).
    /// </summary>
    public double Feedback
    {
        get => FeedbackSlider.Value;
        set => FeedbackSlider.Value = Math.Clamp(value, 0, 100);
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
    /// Gets or sets whether tempo sync is enabled.
    /// </summary>
    public bool IsSync
    {
        get => SyncToggle.IsChecked == true;
        set => SyncToggle.IsChecked = value;
    }

    /// <summary>
    /// Gets or sets whether ping-pong mode is enabled.
    /// </summary>
    public bool IsPingPong
    {
        get => PingPongToggle.IsChecked == true;
        set => PingPongToggle.IsChecked = value;
    }

    /// <summary>
    /// Creates a new DelayControl.
    /// </summary>
    public DelayControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateValueDisplays()
    {
        if (TimeValue != null) TimeValue.Text = $"{TimeSlider.Value:F0} ms";
        if (FeedbackValue != null) FeedbackValue.Text = $"{FeedbackSlider.Value:F0}%";
        if (MixValue != null) MixValue.Text = $"{MixSlider.Value:F0}%";
    }

    private void TimeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (TimeValue == null) return;
        TimeValue.Text = $"{e.NewValue:F0} ms";
        ParameterChanged?.Invoke(this, new DelayParameterChangedEventArgs("Time", e.NewValue));
    }

    private void FeedbackSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (FeedbackValue == null) return;
        FeedbackValue.Text = $"{e.NewValue:F0}%";
        ParameterChanged?.Invoke(this, new DelayParameterChangedEventArgs("Feedback", e.NewValue));
    }

    private void MixSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (MixValue == null) return;
        MixValue.Text = $"{e.NewValue:F0}%";
        ParameterChanged?.Invoke(this, new DelayParameterChangedEventArgs("Mix", e.NewValue));
    }

    private void SyncToggle_Click(object sender, RoutedEventArgs e)
    {
        ParameterChanged?.Invoke(this, new DelayParameterChangedEventArgs("Sync", SyncToggle.IsChecked == true ? 1 : 0));
    }

    private void PingPongToggle_Click(object sender, RoutedEventArgs e)
    {
        ParameterChanged?.Invoke(this, new DelayParameterChangedEventArgs("PingPong", PingPongToggle.IsChecked == true ? 1 : 0));
    }

    /// <summary>
    /// Resets all parameters to defaults.
    /// </summary>
    public void Reset()
    {
        TimeSlider.Value = 250;
        FeedbackSlider.Value = 40;
        MixSlider.Value = 30;
        SyncToggle.IsChecked = false;
        PingPongToggle.IsChecked = false;
    }
}

/// <summary>
/// Event arguments for delay parameter changes.
/// </summary>
public class DelayParameterChangedEventArgs : EventArgs
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
    public DelayParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

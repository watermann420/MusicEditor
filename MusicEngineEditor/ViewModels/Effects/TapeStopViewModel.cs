// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Tape Stop effect control.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Effects;

/// <summary>
/// Tape stop/start direction mode.
/// </summary>
public enum TapeStopDirection
{
    Stop,
    Start
}

/// <summary>
/// ViewModel for the Tape Stop effect.
/// </summary>
public partial class TapeStopViewModel : ViewModelBase
{
    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<string>? ParameterChanged;

    /// <summary>
    /// Event raised when the effect is triggered.
    /// </summary>
    public event EventHandler? EffectTriggered;

    [ObservableProperty]
    private double _stopTime = 500.0;

    [ObservableProperty]
    private double _startTime = 300.0;

    [ObservableProperty]
    private TapeStopDirection _direction = TapeStopDirection.Stop;

    [ObservableProperty]
    private double _wowFlutter = 25.0;

    [ObservableProperty]
    private bool _isTriggered;

    /// <summary>
    /// Gets the minimum stop time in milliseconds.
    /// </summary>
    public double MinStopTime => 10.0;

    /// <summary>
    /// Gets the maximum stop time in milliseconds.
    /// </summary>
    public double MaxStopTime => 5000.0;

    /// <summary>
    /// Gets the minimum start time in milliseconds.
    /// </summary>
    public double MinStartTime => 10.0;

    /// <summary>
    /// Gets the maximum start time in milliseconds.
    /// </summary>
    public double MaxStartTime => 5000.0;

    /// <summary>
    /// Gets the minimum wow/flutter amount.
    /// </summary>
    public double MinWowFlutter => 0.0;

    /// <summary>
    /// Gets the maximum wow/flutter amount.
    /// </summary>
    public double MaxWowFlutter => 100.0;

    /// <summary>
    /// Gets the formatted stop time display.
    /// </summary>
    public string StopTimeDisplay => StopTime < 1000
        ? $"{StopTime:F0} ms"
        : $"{StopTime / 1000:F2} s";

    /// <summary>
    /// Gets the formatted start time display.
    /// </summary>
    public string StartTimeDisplay => StartTime < 1000
        ? $"{StartTime:F0} ms"
        : $"{StartTime / 1000:F2} s";

    /// <summary>
    /// Gets the formatted wow/flutter display.
    /// </summary>
    public string WowFlutterDisplay => $"{WowFlutter:F0}%";

    /// <summary>
    /// Gets whether the direction is set to Stop.
    /// </summary>
    public bool IsStopDirection => Direction == TapeStopDirection.Stop;

    partial void OnStopTimeChanged(double value)
    {
        OnPropertyChanged(nameof(StopTimeDisplay));
        RaiseParameterChanged(nameof(StopTime));
    }

    partial void OnStartTimeChanged(double value)
    {
        OnPropertyChanged(nameof(StartTimeDisplay));
        RaiseParameterChanged(nameof(StartTime));
    }

    partial void OnDirectionChanged(TapeStopDirection value)
    {
        OnPropertyChanged(nameof(IsStopDirection));
        RaiseParameterChanged(nameof(Direction));
    }

    partial void OnWowFlutterChanged(double value)
    {
        OnPropertyChanged(nameof(WowFlutterDisplay));
        RaiseParameterChanged(nameof(WowFlutter));
    }

    [RelayCommand]
    private void Trigger()
    {
        IsTriggered = true;
        EffectTriggered?.Invoke(this, EventArgs.Empty);

        // Reset trigger state after a short delay would be handled by the audio engine
    }

    [RelayCommand]
    private void ToggleDirection()
    {
        Direction = Direction == TapeStopDirection.Stop
            ? TapeStopDirection.Start
            : TapeStopDirection.Stop;
    }

    [RelayCommand]
    private void Reset()
    {
        StopTime = 500.0;
        StartTime = 300.0;
        Direction = TapeStopDirection.Stop;
        WowFlutter = 25.0;
        IsTriggered = false;
        StatusMessage = "Reset to defaults";
    }

    private void RaiseParameterChanged(string parameterName)
    {
        ParameterChanged?.Invoke(this, parameterName);
    }

    /// <summary>
    /// Sets all parameters at once.
    /// </summary>
    public void SetParameters(double stopTime, double startTime, TapeStopDirection direction, double wowFlutter)
    {
        _stopTime = Math.Clamp(stopTime, MinStopTime, MaxStopTime);
        _startTime = Math.Clamp(startTime, MinStartTime, MaxStartTime);
        _direction = direction;
        _wowFlutter = Math.Clamp(wowFlutter, MinWowFlutter, MaxWowFlutter);

        OnPropertyChanged(nameof(StopTime));
        OnPropertyChanged(nameof(StartTime));
        OnPropertyChanged(nameof(Direction));
        OnPropertyChanged(nameof(WowFlutter));
        OnPropertyChanged(nameof(StopTimeDisplay));
        OnPropertyChanged(nameof(StartTimeDisplay));
        OnPropertyChanged(nameof(WowFlutterDisplay));
        OnPropertyChanged(nameof(IsStopDirection));
    }
}

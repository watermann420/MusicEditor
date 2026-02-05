// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Exciter effect control.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Effects;

/// <summary>
/// ViewModel for the Exciter effect.
/// </summary>
public partial class ExciterViewModel : ViewModelBase
{
    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<string>? ParameterChanged;

    [ObservableProperty]
    private double _lowFrequency = 200.0;

    [ObservableProperty]
    private double _highFrequency = 8000.0;

    [ObservableProperty]
    private double _harmonicAmount = 40.0;

    [ObservableProperty]
    private double _brightness = 50.0;

    [ObservableProperty]
    private double _mix = 100.0;

    /// <summary>
    /// Gets the minimum low frequency in Hz.
    /// </summary>
    public double MinLowFrequency => 20.0;

    /// <summary>
    /// Gets the maximum low frequency in Hz.
    /// </summary>
    public double MaxLowFrequency => 2000.0;

    /// <summary>
    /// Gets the minimum high frequency in Hz.
    /// </summary>
    public double MinHighFrequency => 1000.0;

    /// <summary>
    /// Gets the maximum high frequency in Hz.
    /// </summary>
    public double MaxHighFrequency => 20000.0;

    /// <summary>
    /// Gets the minimum percentage value.
    /// </summary>
    public double MinPercentage => 0.0;

    /// <summary>
    /// Gets the maximum percentage value.
    /// </summary>
    public double MaxPercentage => 100.0;

    /// <summary>
    /// Gets the formatted low frequency display.
    /// </summary>
    public string LowFrequencyDisplay => LowFrequency >= 1000
        ? $"{LowFrequency / 1000:F2} kHz"
        : $"{LowFrequency:F0} Hz";

    /// <summary>
    /// Gets the formatted high frequency display.
    /// </summary>
    public string HighFrequencyDisplay => HighFrequency >= 1000
        ? $"{HighFrequency / 1000:F1} kHz"
        : $"{HighFrequency:F0} Hz";

    /// <summary>
    /// Gets the formatted frequency range display.
    /// </summary>
    public string FrequencyRangeDisplay => $"{LowFrequencyDisplay} - {HighFrequencyDisplay}";

    /// <summary>
    /// Gets the formatted harmonic amount display.
    /// </summary>
    public string HarmonicAmountDisplay => $"{HarmonicAmount:F0}%";

    /// <summary>
    /// Gets the formatted brightness display.
    /// </summary>
    public string BrightnessDisplay
    {
        get
        {
            if (Brightness < 35)
                return $"Warm ({Brightness:F0}%)";
            if (Brightness > 65)
                return $"Air ({Brightness:F0}%)";
            return $"Neutral ({Brightness:F0}%)";
        }
    }

    /// <summary>
    /// Gets the formatted mix display.
    /// </summary>
    public string MixDisplay => $"{Mix:F0}%";

    partial void OnLowFrequencyChanged(double value)
    {
        // Ensure low frequency doesn't exceed high frequency
        if (value >= HighFrequency)
        {
            _lowFrequency = HighFrequency - 100;
            OnPropertyChanged(nameof(LowFrequency));
        }
        OnPropertyChanged(nameof(LowFrequencyDisplay));
        OnPropertyChanged(nameof(FrequencyRangeDisplay));
        RaiseParameterChanged(nameof(LowFrequency));
    }

    partial void OnHighFrequencyChanged(double value)
    {
        // Ensure high frequency doesn't go below low frequency
        if (value <= LowFrequency)
        {
            _highFrequency = LowFrequency + 100;
            OnPropertyChanged(nameof(HighFrequency));
        }
        OnPropertyChanged(nameof(HighFrequencyDisplay));
        OnPropertyChanged(nameof(FrequencyRangeDisplay));
        RaiseParameterChanged(nameof(HighFrequency));
    }

    partial void OnHarmonicAmountChanged(double value)
    {
        OnPropertyChanged(nameof(HarmonicAmountDisplay));
        RaiseParameterChanged(nameof(HarmonicAmount));
    }

    partial void OnBrightnessChanged(double value)
    {
        OnPropertyChanged(nameof(BrightnessDisplay));
        RaiseParameterChanged(nameof(Brightness));
    }

    partial void OnMixChanged(double value)
    {
        OnPropertyChanged(nameof(MixDisplay));
        RaiseParameterChanged(nameof(Mix));
    }

    [RelayCommand]
    private void SetFrequencyPreset(string preset)
    {
        switch (preset?.ToLowerInvariant())
        {
            case "low":
            case "bass":
                LowFrequency = 40.0;
                HighFrequency = 500.0;
                break;
            case "mid":
            case "presence":
                LowFrequency = 500.0;
                HighFrequency = 4000.0;
                break;
            case "high":
            case "air":
                LowFrequency = 4000.0;
                HighFrequency = 16000.0;
                break;
            case "full":
            case "wide":
                LowFrequency = 100.0;
                HighFrequency = 12000.0;
                break;
        }
        StatusMessage = $"Frequency range: {preset}";
    }

    [RelayCommand]
    private void Reset()
    {
        LowFrequency = 200.0;
        HighFrequency = 8000.0;
        HarmonicAmount = 40.0;
        Brightness = 50.0;
        Mix = 100.0;
        StatusMessage = "Reset to defaults";
    }

    [RelayCommand]
    private void LoadPreset(string presetName)
    {
        switch (presetName?.ToLowerInvariant())
        {
            case "subtle":
                LowFrequency = 500.0;
                HighFrequency = 8000.0;
                HarmonicAmount = 25.0;
                Brightness = 45.0;
                Mix = 60.0;
                break;
            case "presence":
                LowFrequency = 2000.0;
                HighFrequency = 10000.0;
                HarmonicAmount = 50.0;
                Brightness = 60.0;
                Mix = 80.0;
                break;
            case "air":
                LowFrequency = 6000.0;
                HighFrequency = 16000.0;
                HarmonicAmount = 60.0;
                Brightness = 75.0;
                Mix = 70.0;
                break;
            case "bass":
                LowFrequency = 60.0;
                HighFrequency = 400.0;
                HarmonicAmount = 45.0;
                Brightness = 30.0;
                Mix = 75.0;
                break;
            case "aggressive":
                LowFrequency = 200.0;
                HighFrequency = 12000.0;
                HarmonicAmount = 80.0;
                Brightness = 70.0;
                Mix = 100.0;
                break;
            default:
                Reset();
                break;
        }
        StatusMessage = $"Loaded preset: {presetName}";
    }

    private void RaiseParameterChanged(string parameterName)
    {
        ParameterChanged?.Invoke(this, parameterName);
    }

    /// <summary>
    /// Sets all parameters at once.
    /// </summary>
    public void SetParameters(double lowFreq, double highFreq, double harmonics, double brightness, double mix)
    {
        var clampedLow = Math.Clamp(lowFreq, MinLowFrequency, MaxLowFrequency);
        var clampedHigh = Math.Clamp(highFreq, MinHighFrequency, MaxHighFrequency);

        // Ensure valid range
        if (clampedLow >= clampedHigh)
        {
            clampedHigh = clampedLow + 100;
        }

        LowFrequency = clampedLow;
        HighFrequency = clampedHigh;
        HarmonicAmount = Math.Clamp(harmonics, MinPercentage, MaxPercentage);
        Brightness = Math.Clamp(brightness, MinPercentage, MaxPercentage);
        Mix = Math.Clamp(mix, MinPercentage, MaxPercentage);
    }
}

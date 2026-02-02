// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Tape Saturation effect control.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Effects;

/// <summary>
/// Tape speed in inches per second (IPS).
/// </summary>
public enum TapeSpeed
{
    Ips7_5,
    Ips15,
    Ips30
}

/// <summary>
/// ViewModel for the Tape Saturation effect.
/// </summary>
public partial class TapeSaturationViewModel : ViewModelBase
{
    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<string>? ParameterChanged;

    [ObservableProperty]
    private double _inputGain = 0.0;

    [ObservableProperty]
    private double _saturationAmount = 40.0;

    [ObservableProperty]
    private TapeSpeed _tapeSpeed = TapeSpeed.Ips15;

    [ObservableProperty]
    private double _bias = 50.0;

    [ObservableProperty]
    private double _highFrequencyRolloff = 30.0;

    [ObservableProperty]
    private double _wowFlutter = 15.0;

    [ObservableProperty]
    private double _noiseFloor = -70.0;

    [ObservableProperty]
    private double _hissAmount = 20.0;

    /// <summary>
    /// Gets the minimum input gain in dB.
    /// </summary>
    public double MinInputGain => -24.0;

    /// <summary>
    /// Gets the maximum input gain in dB.
    /// </summary>
    public double MaxInputGain => 24.0;

    /// <summary>
    /// Gets the minimum percentage value.
    /// </summary>
    public double MinPercentage => 0.0;

    /// <summary>
    /// Gets the maximum percentage value.
    /// </summary>
    public double MaxPercentage => 100.0;

    /// <summary>
    /// Gets the minimum noise floor in dB.
    /// </summary>
    public double MinNoiseFloor => -96.0;

    /// <summary>
    /// Gets the maximum noise floor in dB.
    /// </summary>
    public double MaxNoiseFloor => -30.0;

    /// <summary>
    /// Gets the formatted input gain display.
    /// </summary>
    public string InputGainDisplay => $"{InputGain:+0.0;-0.0;0.0} dB";

    /// <summary>
    /// Gets the formatted saturation amount display.
    /// </summary>
    public string SaturationAmountDisplay => $"{SaturationAmount:F0}%";

    /// <summary>
    /// Gets the formatted tape speed display.
    /// </summary>
    public string TapeSpeedDisplay => TapeSpeed switch
    {
        TapeSpeed.Ips7_5 => "7.5 IPS",
        TapeSpeed.Ips15 => "15 IPS",
        TapeSpeed.Ips30 => "30 IPS",
        _ => "15 IPS"
    };

    /// <summary>
    /// Gets the formatted bias display.
    /// </summary>
    public string BiasDisplay => $"{Bias:F0}%";

    /// <summary>
    /// Gets the formatted high frequency rolloff display.
    /// </summary>
    public string HighFrequencyRolloffDisplay => $"{HighFrequencyRolloff:F0}%";

    /// <summary>
    /// Gets the formatted wow/flutter display.
    /// </summary>
    public string WowFlutterDisplay => $"{WowFlutter:F0}%";

    /// <summary>
    /// Gets the formatted noise floor display.
    /// </summary>
    public string NoiseFloorDisplay => $"{NoiseFloor:F0} dB";

    /// <summary>
    /// Gets the formatted hiss amount display.
    /// </summary>
    public string HissAmountDisplay => $"{HissAmount:F0}%";

    /// <summary>
    /// Gets the available tape speed options.
    /// </summary>
    public static TapeSpeed[] AvailableTapeSpeeds => Enum.GetValues<TapeSpeed>();

    partial void OnInputGainChanged(double value)
    {
        OnPropertyChanged(nameof(InputGainDisplay));
        RaiseParameterChanged(nameof(InputGain));
    }

    partial void OnSaturationAmountChanged(double value)
    {
        OnPropertyChanged(nameof(SaturationAmountDisplay));
        RaiseParameterChanged(nameof(SaturationAmount));
    }

    partial void OnTapeSpeedChanged(TapeSpeed value)
    {
        OnPropertyChanged(nameof(TapeSpeedDisplay));
        ApplyTapeSpeedCharacteristics(value);
        RaiseParameterChanged(nameof(TapeSpeed));
    }

    partial void OnBiasChanged(double value)
    {
        OnPropertyChanged(nameof(BiasDisplay));
        RaiseParameterChanged(nameof(Bias));
    }

    partial void OnHighFrequencyRolloffChanged(double value)
    {
        OnPropertyChanged(nameof(HighFrequencyRolloffDisplay));
        RaiseParameterChanged(nameof(HighFrequencyRolloff));
    }

    partial void OnWowFlutterChanged(double value)
    {
        OnPropertyChanged(nameof(WowFlutterDisplay));
        RaiseParameterChanged(nameof(WowFlutter));
    }

    partial void OnNoiseFloorChanged(double value)
    {
        OnPropertyChanged(nameof(NoiseFloorDisplay));
        RaiseParameterChanged(nameof(NoiseFloor));
    }

    partial void OnHissAmountChanged(double value)
    {
        OnPropertyChanged(nameof(HissAmountDisplay));
        RaiseParameterChanged(nameof(HissAmount));
    }

    /// <summary>
    /// Applies characteristic adjustments based on tape speed.
    /// </summary>
    private void ApplyTapeSpeedCharacteristics(TapeSpeed speed)
    {
        // Different tape speeds have different frequency response and noise characteristics
        switch (speed)
        {
            case TapeSpeed.Ips7_5:
                // More high frequency rolloff, more noise
                HighFrequencyRolloff = Math.Max(HighFrequencyRolloff, 40.0);
                break;
            case TapeSpeed.Ips15:
                // Balanced characteristics
                break;
            case TapeSpeed.Ips30:
                // Better high frequency response, less noise
                HighFrequencyRolloff = Math.Min(HighFrequencyRolloff, 25.0);
                break;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        InputGain = 0.0;
        SaturationAmount = 40.0;
        _tapeSpeed = TapeSpeed.Ips15;
        OnPropertyChanged(nameof(TapeSpeed));
        OnPropertyChanged(nameof(TapeSpeedDisplay));
        Bias = 50.0;
        HighFrequencyRolloff = 30.0;
        WowFlutter = 15.0;
        NoiseFloor = -70.0;
        HissAmount = 20.0;
        StatusMessage = "Reset to defaults";
    }

    private void RaiseParameterChanged(string parameterName)
    {
        ParameterChanged?.Invoke(this, parameterName);
    }

    /// <summary>
    /// Sets all parameters at once.
    /// </summary>
    public void SetParameters(double inputGain, double saturation, TapeSpeed speed, double bias,
        double hfRolloff, double wowFlutter, double noiseFloor, double hiss)
    {
        _inputGain = Math.Clamp(inputGain, MinInputGain, MaxInputGain);
        _saturationAmount = Math.Clamp(saturation, MinPercentage, MaxPercentage);
        _tapeSpeed = speed;
        _bias = Math.Clamp(bias, MinPercentage, MaxPercentage);
        _highFrequencyRolloff = Math.Clamp(hfRolloff, MinPercentage, MaxPercentage);
        _wowFlutter = Math.Clamp(wowFlutter, MinPercentage, MaxPercentage);
        _noiseFloor = Math.Clamp(noiseFloor, MinNoiseFloor, MaxNoiseFloor);
        _hissAmount = Math.Clamp(hiss, MinPercentage, MaxPercentage);

        OnPropertyChanged(nameof(InputGain));
        OnPropertyChanged(nameof(SaturationAmount));
        OnPropertyChanged(nameof(TapeSpeed));
        OnPropertyChanged(nameof(Bias));
        OnPropertyChanged(nameof(HighFrequencyRolloff));
        OnPropertyChanged(nameof(WowFlutter));
        OnPropertyChanged(nameof(NoiseFloor));
        OnPropertyChanged(nameof(HissAmount));
        OnPropertyChanged(nameof(InputGainDisplay));
        OnPropertyChanged(nameof(SaturationAmountDisplay));
        OnPropertyChanged(nameof(TapeSpeedDisplay));
        OnPropertyChanged(nameof(BiasDisplay));
        OnPropertyChanged(nameof(HighFrequencyRolloffDisplay));
        OnPropertyChanged(nameof(WowFlutterDisplay));
        OnPropertyChanged(nameof(NoiseFloorDisplay));
        OnPropertyChanged(nameof(HissAmountDisplay));
    }
}

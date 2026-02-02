// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Bitcrusher effect control.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Effects;

/// <summary>
/// ViewModel for the Bitcrusher effect.
/// </summary>
public partial class BitcrusherViewModel : ViewModelBase
{
    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<string>? ParameterChanged;

    [ObservableProperty]
    private int _bitDepth = 16;

    [ObservableProperty]
    private double _sampleRateReduction = 0.0;

    [ObservableProperty]
    private bool _ditherEnabled = false;

    [ObservableProperty]
    private double _jitterAmount = 0.0;

    [ObservableProperty]
    private double _mix = 100.0;

    /// <summary>
    /// Gets the minimum bit depth.
    /// </summary>
    public int MinBitDepth => 1;

    /// <summary>
    /// Gets the maximum bit depth.
    /// </summary>
    public int MaxBitDepth => 24;

    /// <summary>
    /// Gets the minimum sample rate reduction (0 = no reduction).
    /// </summary>
    public double MinSampleRateReduction => 0.0;

    /// <summary>
    /// Gets the maximum sample rate reduction (as divisor, e.g., 100 = 1/100th of original).
    /// </summary>
    public double MaxSampleRateReduction => 100.0;

    /// <summary>
    /// Gets the minimum percentage value.
    /// </summary>
    public double MinPercentage => 0.0;

    /// <summary>
    /// Gets the maximum percentage value.
    /// </summary>
    public double MaxPercentage => 100.0;

    /// <summary>
    /// Gets the formatted bit depth display.
    /// </summary>
    public string BitDepthDisplay => $"{BitDepth} bit";

    /// <summary>
    /// Gets the formatted sample rate reduction display.
    /// </summary>
    public string SampleRateReductionDisplay
    {
        get
        {
            if (SampleRateReduction <= 0)
                return "Off";

            // Calculate effective sample rate (assuming 44100 base)
            double factor = 1.0 + (SampleRateReduction / 10.0);
            double effectiveRate = 44100.0 / factor;

            if (effectiveRate >= 1000)
                return $"{effectiveRate / 1000:F1} kHz";
            return $"{effectiveRate:F0} Hz";
        }
    }

    /// <summary>
    /// Gets the formatted jitter amount display.
    /// </summary>
    public string JitterAmountDisplay => $"{JitterAmount:F0}%";

    /// <summary>
    /// Gets the formatted mix display.
    /// </summary>
    public string MixDisplay => $"{Mix:F0}%";

    /// <summary>
    /// Gets the number of quantization levels based on bit depth.
    /// </summary>
    public int QuantizationLevels => (int)Math.Pow(2, BitDepth);

    partial void OnBitDepthChanged(int value)
    {
        OnPropertyChanged(nameof(BitDepthDisplay));
        OnPropertyChanged(nameof(QuantizationLevels));
        RaiseParameterChanged(nameof(BitDepth));
    }

    partial void OnSampleRateReductionChanged(double value)
    {
        OnPropertyChanged(nameof(SampleRateReductionDisplay));
        RaiseParameterChanged(nameof(SampleRateReduction));
    }

    partial void OnDitherEnabledChanged(bool value)
    {
        RaiseParameterChanged(nameof(DitherEnabled));
    }

    partial void OnJitterAmountChanged(double value)
    {
        OnPropertyChanged(nameof(JitterAmountDisplay));
        RaiseParameterChanged(nameof(JitterAmount));
    }

    partial void OnMixChanged(double value)
    {
        OnPropertyChanged(nameof(MixDisplay));
        RaiseParameterChanged(nameof(Mix));
    }

    [RelayCommand]
    private void SetBitDepthPreset(int bits)
    {
        BitDepth = Math.Clamp(bits, MinBitDepth, MaxBitDepth);
    }

    [RelayCommand]
    private void Reset()
    {
        BitDepth = 16;
        SampleRateReduction = 0.0;
        DitherEnabled = false;
        JitterAmount = 0.0;
        Mix = 100.0;
        StatusMessage = "Reset to defaults";
    }

    [RelayCommand]
    private void LoadPreset(string presetName)
    {
        switch (presetName?.ToLowerInvariant())
        {
            case "8bit":
            case "retro":
                BitDepth = 8;
                SampleRateReduction = 50.0;
                DitherEnabled = false;
                JitterAmount = 5.0;
                Mix = 100.0;
                break;
            case "lofi":
                BitDepth = 12;
                SampleRateReduction = 30.0;
                DitherEnabled = true;
                JitterAmount = 10.0;
                Mix = 80.0;
                break;
            case "phone":
                BitDepth = 8;
                SampleRateReduction = 80.0;
                DitherEnabled = false;
                JitterAmount = 15.0;
                Mix = 100.0;
                break;
            case "subtle":
                BitDepth = 14;
                SampleRateReduction = 10.0;
                DitherEnabled = true;
                JitterAmount = 2.0;
                Mix = 50.0;
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
    public void SetParameters(int bitDepth, double sampleRateReduction, bool dither, double jitter, double mix)
    {
        _bitDepth = Math.Clamp(bitDepth, MinBitDepth, MaxBitDepth);
        _sampleRateReduction = Math.Clamp(sampleRateReduction, MinSampleRateReduction, MaxSampleRateReduction);
        _ditherEnabled = dither;
        _jitterAmount = Math.Clamp(jitter, MinPercentage, MaxPercentage);
        _mix = Math.Clamp(mix, MinPercentage, MaxPercentage);

        OnPropertyChanged(nameof(BitDepth));
        OnPropertyChanged(nameof(SampleRateReduction));
        OnPropertyChanged(nameof(DitherEnabled));
        OnPropertyChanged(nameof(JitterAmount));
        OnPropertyChanged(nameof(Mix));
        OnPropertyChanged(nameof(BitDepthDisplay));
        OnPropertyChanged(nameof(SampleRateReductionDisplay));
        OnPropertyChanged(nameof(JitterAmountDisplay));
        OnPropertyChanged(nameof(MixDisplay));
        OnPropertyChanged(nameof(QuantizationLevels));
    }
}

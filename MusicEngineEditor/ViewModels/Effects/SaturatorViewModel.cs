// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Saturator effect control.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Effects;

/// <summary>
/// Saturation type/character.
/// </summary>
public enum SaturationType
{
    Tube,
    Tape,
    Transistor,
    Digital
}

/// <summary>
/// ViewModel for the Saturator effect.
/// </summary>
public partial class SaturatorViewModel : ViewModelBase
{
    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<string>? ParameterChanged;

    [ObservableProperty]
    private double _drive = 30.0;

    [ObservableProperty]
    private SaturationType _saturationType = SaturationType.Tube;

    [ObservableProperty]
    private double _tone = 50.0;

    [ObservableProperty]
    private double _outputLevel = 0.0;

    [ObservableProperty]
    private double _mix = 100.0;

    /// <summary>
    /// Gets the minimum drive value.
    /// </summary>
    public double MinDrive => 0.0;

    /// <summary>
    /// Gets the maximum drive value.
    /// </summary>
    public double MaxDrive => 100.0;

    /// <summary>
    /// Gets the minimum tone value.
    /// </summary>
    public double MinTone => 0.0;

    /// <summary>
    /// Gets the maximum tone value.
    /// </summary>
    public double MaxTone => 100.0;

    /// <summary>
    /// Gets the minimum output level in dB.
    /// </summary>
    public double MinOutputLevel => -24.0;

    /// <summary>
    /// Gets the maximum output level in dB.
    /// </summary>
    public double MaxOutputLevel => 12.0;

    /// <summary>
    /// Gets the minimum mix value.
    /// </summary>
    public double MinMix => 0.0;

    /// <summary>
    /// Gets the maximum mix value.
    /// </summary>
    public double MaxMix => 100.0;

    /// <summary>
    /// Gets the formatted drive display.
    /// </summary>
    public string DriveDisplay => $"{Drive:F0}%";

    /// <summary>
    /// Gets the formatted tone display.
    /// </summary>
    public string ToneDisplay
    {
        get
        {
            if (Tone < 40)
                return $"Dark ({Tone:F0}%)";
            if (Tone > 60)
                return $"Bright ({Tone:F0}%)";
            return $"Neutral ({Tone:F0}%)";
        }
    }

    /// <summary>
    /// Gets the formatted output level display.
    /// </summary>
    public string OutputLevelDisplay => $"{OutputLevel:+0.0;-0.0;0.0} dB";

    /// <summary>
    /// Gets the formatted mix display.
    /// </summary>
    public string MixDisplay => $"{Mix:F0}%";

    /// <summary>
    /// Gets the saturation type description.
    /// </summary>
    public string SaturationTypeDescription => SaturationType switch
    {
        SaturationType.Tube => "Warm, even harmonics, soft clipping",
        SaturationType.Tape => "Compression, HF rolloff, subtle distortion",
        SaturationType.Transistor => "Bright, odd harmonics, aggressive",
        SaturationType.Digital => "Clean, precise, hard clipping",
        _ => ""
    };

    /// <summary>
    /// Gets the available saturation types.
    /// </summary>
    public static SaturationType[] AvailableSaturationTypes => Enum.GetValues<SaturationType>();

    partial void OnDriveChanged(double value)
    {
        OnPropertyChanged(nameof(DriveDisplay));
        RaiseParameterChanged(nameof(Drive));
    }

    partial void OnSaturationTypeChanged(SaturationType value)
    {
        OnPropertyChanged(nameof(SaturationTypeDescription));
        RaiseParameterChanged(nameof(SaturationType));
    }

    partial void OnToneChanged(double value)
    {
        OnPropertyChanged(nameof(ToneDisplay));
        RaiseParameterChanged(nameof(Tone));
    }

    partial void OnOutputLevelChanged(double value)
    {
        OnPropertyChanged(nameof(OutputLevelDisplay));
        RaiseParameterChanged(nameof(OutputLevel));
    }

    partial void OnMixChanged(double value)
    {
        OnPropertyChanged(nameof(MixDisplay));
        RaiseParameterChanged(nameof(Mix));
    }

    [RelayCommand]
    private void Reset()
    {
        Drive = 30.0;
        SaturationType = SaturationType.Tube;
        Tone = 50.0;
        OutputLevel = 0.0;
        Mix = 100.0;
        StatusMessage = "Reset to defaults";
    }

    [RelayCommand]
    private void LoadPreset(string presetName)
    {
        switch (presetName?.ToLowerInvariant())
        {
            case "warm":
            case "tube":
                Drive = 40.0;
                SaturationType = SaturationType.Tube;
                Tone = 45.0;
                OutputLevel = -2.0;
                Mix = 75.0;
                break;
            case "tape":
            case "vintage":
                Drive = 35.0;
                SaturationType = SaturationType.Tape;
                Tone = 40.0;
                OutputLevel = -1.0;
                Mix = 80.0;
                break;
            case "edge":
            case "transistor":
                Drive = 55.0;
                SaturationType = SaturationType.Transistor;
                Tone = 60.0;
                OutputLevel = -3.0;
                Mix = 70.0;
                break;
            case "crunch":
            case "heavy":
                Drive = 80.0;
                SaturationType = SaturationType.Digital;
                Tone = 55.0;
                OutputLevel = -6.0;
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
    public void SetParameters(double drive, SaturationType type, double tone, double output, double mix)
    {
        _drive = Math.Clamp(drive, MinDrive, MaxDrive);
        _saturationType = type;
        _tone = Math.Clamp(tone, MinTone, MaxTone);
        _outputLevel = Math.Clamp(output, MinOutputLevel, MaxOutputLevel);
        _mix = Math.Clamp(mix, MinMix, MaxMix);

        OnPropertyChanged(nameof(Drive));
        OnPropertyChanged(nameof(SaturationType));
        OnPropertyChanged(nameof(Tone));
        OnPropertyChanged(nameof(OutputLevel));
        OnPropertyChanged(nameof(Mix));
        OnPropertyChanged(nameof(DriveDisplay));
        OnPropertyChanged(nameof(SaturationTypeDescription));
        OnPropertyChanged(nameof(ToneDisplay));
        OnPropertyChanged(nameof(OutputLevelDisplay));
        OnPropertyChanged(nameof(MixDisplay));
    }
}

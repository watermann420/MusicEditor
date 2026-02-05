// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Vinyl Emulation effect control.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Effects;

/// <summary>
/// Vinyl record age/condition presets.
/// </summary>
public enum VinylAge
{
    New,
    Used,
    Vintage,
    Damaged
}

/// <summary>
/// Vinyl RPM speed settings.
/// </summary>
public enum VinylRpm
{
    Rpm33,
    Rpm45,
    Rpm78
}

/// <summary>
/// ViewModel for the Vinyl Emulation effect.
/// </summary>
public partial class VinylEmulationViewModel : ViewModelBase
{
    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<string>? ParameterChanged;

    [ObservableProperty]
    private double _crackleAmount = 30.0;

    [ObservableProperty]
    private double _popFrequency = 20.0;

    [ObservableProperty]
    private double _dustAmount = 15.0;

    [ObservableProperty]
    private double _surfaceNoise = 25.0;

    [ObservableProperty]
    private double _warpAmount = 10.0;

    [ObservableProperty]
    private VinylAge _age = VinylAge.Used;

    [ObservableProperty]
    private VinylRpm _rpm = VinylRpm.Rpm33;

    /// <summary>
    /// Gets the minimum value for all percentage sliders.
    /// </summary>
    public double MinValue => 0.0;

    /// <summary>
    /// Gets the maximum value for all percentage sliders.
    /// </summary>
    public double MaxValue => 100.0;

    /// <summary>
    /// Gets the formatted crackle amount display.
    /// </summary>
    public string CrackleAmountDisplay => $"{CrackleAmount:F0}%";

    /// <summary>
    /// Gets the formatted pop frequency display.
    /// </summary>
    public string PopFrequencyDisplay => $"{PopFrequency:F0}%";

    /// <summary>
    /// Gets the formatted dust amount display.
    /// </summary>
    public string DustAmountDisplay => $"{DustAmount:F0}%";

    /// <summary>
    /// Gets the formatted surface noise display.
    /// </summary>
    public string SurfaceNoiseDisplay => $"{SurfaceNoise:F0}%";

    /// <summary>
    /// Gets the formatted warp amount display.
    /// </summary>
    public string WarpAmountDisplay => $"{WarpAmount:F0}%";

    /// <summary>
    /// Gets the RPM display string.
    /// </summary>
    public string RpmDisplay => Rpm switch
    {
        VinylRpm.Rpm33 => "33 RPM",
        VinylRpm.Rpm45 => "45 RPM",
        VinylRpm.Rpm78 => "78 RPM",
        _ => "33 RPM"
    };

    /// <summary>
    /// Gets the available age options.
    /// </summary>
    public static VinylAge[] AvailableAges => Enum.GetValues<VinylAge>();

    /// <summary>
    /// Gets the available RPM options.
    /// </summary>
    public static VinylRpm[] AvailableRpms => Enum.GetValues<VinylRpm>();

    partial void OnCrackleAmountChanged(double value)
    {
        OnPropertyChanged(nameof(CrackleAmountDisplay));
        RaiseParameterChanged(nameof(CrackleAmount));
    }

    partial void OnPopFrequencyChanged(double value)
    {
        OnPropertyChanged(nameof(PopFrequencyDisplay));
        RaiseParameterChanged(nameof(PopFrequency));
    }

    partial void OnDustAmountChanged(double value)
    {
        OnPropertyChanged(nameof(DustAmountDisplay));
        RaiseParameterChanged(nameof(DustAmount));
    }

    partial void OnSurfaceNoiseChanged(double value)
    {
        OnPropertyChanged(nameof(SurfaceNoiseDisplay));
        RaiseParameterChanged(nameof(SurfaceNoise));
    }

    partial void OnWarpAmountChanged(double value)
    {
        OnPropertyChanged(nameof(WarpAmountDisplay));
        RaiseParameterChanged(nameof(WarpAmount));
    }

    partial void OnAgeChanged(VinylAge value)
    {
        ApplyAgePreset(value);
        RaiseParameterChanged(nameof(Age));
    }

    partial void OnRpmChanged(VinylRpm value)
    {
        OnPropertyChanged(nameof(RpmDisplay));
        RaiseParameterChanged(nameof(Rpm));
    }

    /// <summary>
    /// Applies preset values based on vinyl age selection.
    /// </summary>
    private void ApplyAgePreset(VinylAge age)
    {
        switch (age)
        {
            case VinylAge.New:
                CrackleAmount = 5.0;
                PopFrequency = 2.0;
                DustAmount = 3.0;
                SurfaceNoise = 8.0;
                WarpAmount = 0.0;
                break;
            case VinylAge.Used:
                CrackleAmount = 30.0;
                PopFrequency = 20.0;
                DustAmount = 15.0;
                SurfaceNoise = 25.0;
                WarpAmount = 10.0;
                break;
            case VinylAge.Vintage:
                CrackleAmount = 50.0;
                PopFrequency = 40.0;
                DustAmount = 35.0;
                SurfaceNoise = 45.0;
                WarpAmount = 25.0;
                break;
            case VinylAge.Damaged:
                CrackleAmount = 75.0;
                PopFrequency = 60.0;
                DustAmount = 55.0;
                SurfaceNoise = 70.0;
                WarpAmount = 45.0;
                break;
        }
    }

    [RelayCommand]
    private void Reset()
    {
        CrackleAmount = 30.0;
        PopFrequency = 20.0;
        DustAmount = 15.0;
        SurfaceNoise = 25.0;
        WarpAmount = 10.0;
        Age = VinylAge.Used;
        Rpm = VinylRpm.Rpm33;
        StatusMessage = "Reset to defaults";
    }

    private void RaiseParameterChanged(string parameterName)
    {
        ParameterChanged?.Invoke(this, parameterName);
    }

    /// <summary>
    /// Sets all parameters at once.
    /// </summary>
    public void SetParameters(double crackle, double pop, double dust, double surface, double warp, VinylAge age, VinylRpm rpm)
    {
        CrackleAmount = Math.Clamp(crackle, MinValue, MaxValue);
        PopFrequency = Math.Clamp(pop, MinValue, MaxValue);
        DustAmount = Math.Clamp(dust, MinValue, MaxValue);
        SurfaceNoise = Math.Clamp(surface, MinValue, MaxValue);
        WarpAmount = Math.Clamp(warp, MinValue, MaxValue);
        Age = age;
        Rpm = rpm;
    }
}

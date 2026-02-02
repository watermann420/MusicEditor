// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Vinyl Emulation effect control with crackle, pops, dust, surface noise, warp, age, and RPM settings.

using System;
using System.Windows;
using System.Windows.Controls;

namespace MusicEngineEditor.Controls.Effects;

/// <summary>
/// Vinyl Emulation effect control with crackle, pops, dust, surface noise,
/// warp, age condition selector, and RPM speed selector.
/// </summary>
public partial class VinylEmulationControl : UserControl
{
    private bool _isUpdating;

    /// <summary>
    /// Event raised when any parameter changes.
    /// </summary>
    public event EventHandler<VinylParameterChangedEventArgs>? ParameterChanged;

    /// <summary>
    /// Gets or sets the crackle amount (0-100%).
    /// </summary>
    public double CrackleAmount
    {
        get => CrackleSlider.Value;
        set => CrackleSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the pop frequency (0-100%).
    /// </summary>
    public double PopFrequency
    {
        get => PopSlider.Value;
        set => PopSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the dust amount (0-100%).
    /// </summary>
    public double DustAmount
    {
        get => DustSlider.Value;
        set => DustSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the surface noise level (0-100%).
    /// </summary>
    public double SurfaceNoise
    {
        get => SurfaceNoiseSlider.Value;
        set => SurfaceNoiseSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the warp amount (0-100%).
    /// </summary>
    public double WarpAmount
    {
        get => WarpSlider.Value;
        set => WarpSlider.Value = Math.Clamp(value, 0, 100);
    }

    /// <summary>
    /// Gets or sets the vinyl age/condition (0=New, 1=Used, 2=Vintage, 3=Damaged).
    /// </summary>
    public int Age
    {
        get => AgeCombo.SelectedIndex;
        set => AgeCombo.SelectedIndex = Math.Clamp(value, 0, 3);
    }

    /// <summary>
    /// Gets the age as a string.
    /// </summary>
    public string AgeName => AgeCombo.SelectedItem is ComboBoxItem item
        ? item.Content?.ToString() ?? "Used"
        : "Used";

    /// <summary>
    /// Gets or sets the RPM speed (0=33, 1=45, 2=78).
    /// </summary>
    public int Rpm
    {
        get
        {
            if (Rpm33Radio.IsChecked == true) return 33;
            if (Rpm45Radio.IsChecked == true) return 45;
            return 78;
        }
        set
        {
            Rpm33Radio.IsChecked = value == 33;
            Rpm45Radio.IsChecked = value == 45;
            Rpm78Radio.IsChecked = value == 78;
        }
    }

    /// <summary>
    /// Creates a new VinylEmulationControl.
    /// </summary>
    public VinylEmulationControl()
    {
        InitializeComponent();
        UpdateValueDisplays();
    }

    private void UpdateValueDisplays()
    {
        CrackleValue.Text = $"{CrackleSlider.Value:F0}%";
        PopValue.Text = $"{PopSlider.Value:F0}%";
        DustValue.Text = $"{DustSlider.Value:F0}%";
        SurfaceNoiseValue.Text = $"{SurfaceNoiseSlider.Value:F0}%";
        WarpValue.Text = $"{WarpSlider.Value:F0}%";
    }

    private void AgeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdating) return;

        ApplyAgePreset(AgeCombo.SelectedIndex);
        RaiseParameterChanged("Age", AgeCombo.SelectedIndex);
    }

    private void ApplyAgePreset(int ageIndex)
    {
        _isUpdating = true;
        try
        {
            switch (ageIndex)
            {
                case 0: // New
                    CrackleSlider.Value = 5;
                    PopSlider.Value = 2;
                    DustSlider.Value = 3;
                    SurfaceNoiseSlider.Value = 8;
                    WarpSlider.Value = 0;
                    break;
                case 1: // Used
                    CrackleSlider.Value = 30;
                    PopSlider.Value = 20;
                    DustSlider.Value = 15;
                    SurfaceNoiseSlider.Value = 25;
                    WarpSlider.Value = 10;
                    break;
                case 2: // Vintage
                    CrackleSlider.Value = 50;
                    PopSlider.Value = 40;
                    DustSlider.Value = 35;
                    SurfaceNoiseSlider.Value = 45;
                    WarpSlider.Value = 25;
                    break;
                case 3: // Damaged
                    CrackleSlider.Value = 75;
                    PopSlider.Value = 60;
                    DustSlider.Value = 55;
                    SurfaceNoiseSlider.Value = 70;
                    WarpSlider.Value = 45;
                    break;
            }
            UpdateValueDisplays();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void RpmRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (_isUpdating) return;

        RaiseParameterChanged("Rpm", Rpm);
    }

    private void CrackleSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (CrackleValue == null || _isUpdating) return;

        CrackleValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Crackle", e.NewValue);
    }

    private void PopSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (PopValue == null || _isUpdating) return;

        PopValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Pop", e.NewValue);
    }

    private void DustSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DustValue == null || _isUpdating) return;

        DustValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Dust", e.NewValue);
    }

    private void SurfaceNoiseSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SurfaceNoiseValue == null || _isUpdating) return;

        SurfaceNoiseValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("SurfaceNoise", e.NewValue);
    }

    private void WarpSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (WarpValue == null || _isUpdating) return;

        WarpValue.Text = $"{e.NewValue:F0}%";
        RaiseParameterChanged("Warp", e.NewValue);
    }

    private void RaiseParameterChanged(string parameterName, double value)
    {
        ParameterChanged?.Invoke(this, new VinylParameterChangedEventArgs(parameterName, value));
    }

    /// <summary>
    /// Sets all parameters at once without triggering individual change events.
    /// </summary>
    public void SetParameters(double crackle, double pop, double dust, double surfaceNoise, double warp, int age, int rpm)
    {
        _isUpdating = true;
        try
        {
            CrackleSlider.Value = Math.Clamp(crackle, 0, 100);
            PopSlider.Value = Math.Clamp(pop, 0, 100);
            DustSlider.Value = Math.Clamp(dust, 0, 100);
            SurfaceNoiseSlider.Value = Math.Clamp(surfaceNoise, 0, 100);
            WarpSlider.Value = Math.Clamp(warp, 0, 100);
            AgeCombo.SelectedIndex = Math.Clamp(age, 0, 3);
            Rpm = rpm;
            UpdateValueDisplays();
        }
        finally
        {
            _isUpdating = false;
        }
    }

    /// <summary>
    /// Resets all parameters to defaults (Used condition).
    /// </summary>
    public void Reset()
    {
        SetParameters(30, 20, 15, 25, 10, 1, 33);
    }
}

/// <summary>
/// Event arguments for vinyl parameter changes.
/// </summary>
public class VinylParameterChangedEventArgs : EventArgs
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
    public VinylParameterChangedEventArgs(string parameterName, double value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

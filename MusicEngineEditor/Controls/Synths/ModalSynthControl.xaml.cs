// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Modal Synthesizer control.

using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using MusicEngine.Core.Synthesizers;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for ModalSynthControl.xaml.
/// Provides a visual editor for modal synthesis with resonant modes,
/// material selection, and exciter configuration.
/// </summary>
public partial class ModalSynthControl : UserControl
{
    private ModalSynth? _synth;

    /// <summary>
    /// Creates a new ModalSynthControl.
    /// </summary>
    public ModalSynthControl()
    {
        InitializeComponent();
        DataContext = new ModalSynthViewModel();
    }

    /// <summary>
    /// Gets or sets the modal synth instance being edited.
    /// </summary>
    public ModalSynth? Synth
    {
        get => _synth;
        set
        {
            _synth = value;
            if (_synth != null && DataContext is ModalSynthViewModel vm)
            {
                vm.LoadFromSynth(_synth);
            }
        }
    }

    private void AddMode_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModalSynthViewModel vm)
        {
            vm.AddMode();
            ApplyToSynth();
        }
    }

    private void RemoveMode_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModalSynthViewModel vm && vm.Modes.Count > 1)
        {
            vm.RemoveLastMode();
            ApplyToSynth();
        }
    }

    private void ResetModes_Click(object sender, RoutedEventArgs e)
    {
        if (DataContext is ModalSynthViewModel vm)
        {
            vm.ResetModes();
            ApplyToSynth();
        }
    }

    private void LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string presetName && DataContext is ModalSynthViewModel vm)
        {
            vm.LoadPreset(presetName);
            ApplyToSynth();
        }
    }

    private void ApplyToSynth()
    {
        if (_synth != null && DataContext is ModalSynthViewModel vm)
        {
            vm.ApplyToSynth(_synth);
        }
    }
}

/// <summary>
/// ViewModel for modal synthesis parameters.
/// </summary>
public class ModalSynthViewModel : INotifyPropertyChanged
{
    private float _volume = 0.5f;
    private int _exciterIndex;
    private float _exciterLevel = 1.0f;
    private float _exciterDecay = 1.0f;
    private int _materialIndex;
    private float _stiffness = 0.1f;
    private float _brightness = 0.5f;
    private float _decayScale = 1.0f;
    private string _statusMessage = "Ready";
    private string _materialName = "Steel";

    /// <summary>
    /// Collection of resonant modes.
    /// </summary>
    public ObservableCollection<ResonantModeViewModel> Modes { get; } = new();

    /// <summary>
    /// Gets or sets the master volume.
    /// </summary>
    public float Volume
    {
        get => _volume;
        set { _volume = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the exciter type index.
    /// </summary>
    public int ExciterIndex
    {
        get => _exciterIndex;
        set { _exciterIndex = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the exciter level.
    /// </summary>
    public float ExciterLevel
    {
        get => _exciterLevel;
        set { _exciterLevel = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the exciter decay.
    /// </summary>
    public float ExciterDecay
    {
        get => _exciterDecay;
        set { _exciterDecay = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the material index.
    /// </summary>
    public int MaterialIndex
    {
        get => _materialIndex;
        set
        {
            _materialIndex = value;
            OnPropertyChanged();
            UpdateMaterialName();
        }
    }

    /// <summary>
    /// Gets or sets the stiffness (inharmonicity).
    /// </summary>
    public float Stiffness
    {
        get => _stiffness;
        set { _stiffness = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the brightness.
    /// </summary>
    public float Brightness
    {
        get => _brightness;
        set { _brightness = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the global decay scale.
    /// </summary>
    public float DecayScale
    {
        get => _decayScale;
        set { _decayScale = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets the current status message.
    /// </summary>
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets the current material name.
    /// </summary>
    public string MaterialName
    {
        get => _materialName;
        private set { _materialName = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets the number of modes.
    /// </summary>
    public int ModeCount => Modes.Count;

    /// <summary>
    /// Creates a new ModalSynthViewModel with default values.
    /// </summary>
    public ModalSynthViewModel()
    {
        // Initialize with default steel modes
        ResetModes();
    }

    /// <summary>
    /// Loads parameters from a ModalSynth instance.
    /// </summary>
    public void LoadFromSynth(ModalSynth synth)
    {
        Volume = synth.Volume;
        ExciterIndex = (int)synth.Exciter;
        ExciterLevel = synth.ExciterLevel;
        ExciterDecay = synth.ExciterDecay;
        MaterialIndex = (int)synth.Material;
        Stiffness = synth.Stiffness;
        Brightness = synth.Brightness;
        DecayScale = synth.DecayScale;

        Modes.Clear();
        for (int i = 0; i < synth.Modes.Count; i++)
        {
            var mode = synth.Modes[i];
            Modes.Add(new ResonantModeViewModel
            {
                Index = i + 1,
                FrequencyRatio = mode.FrequencyRatio,
                Amplitude = mode.Amplitude,
                DecayTime = mode.DecayTime
            });
        }
        OnPropertyChanged(nameof(ModeCount));
        StatusMessage = $"Loaded {synth.Name}";
    }

    /// <summary>
    /// Applies current parameters to a ModalSynth instance.
    /// </summary>
    public void ApplyToSynth(ModalSynth synth)
    {
        synth.Volume = Volume;
        synth.Exciter = (ModalExciter)ExciterIndex;
        synth.ExciterLevel = ExciterLevel;
        synth.ExciterDecay = ExciterDecay;
        synth.Material = (ModalMaterial)MaterialIndex;
        synth.Stiffness = Stiffness;
        synth.Brightness = Brightness;
        synth.DecayScale = DecayScale;

        synth.Modes.Clear();
        foreach (var modeVm in Modes)
        {
            synth.Modes.Add(new ResonantMode(modeVm.FrequencyRatio, modeVm.Amplitude, modeVm.DecayTime));
        }
    }

    /// <summary>
    /// Adds a new resonant mode.
    /// </summary>
    public void AddMode()
    {
        int nextRatio = Modes.Count + 1;
        Modes.Add(new ResonantModeViewModel
        {
            Index = Modes.Count + 1,
            FrequencyRatio = nextRatio,
            Amplitude = Math.Max(0.1, 1.0 - nextRatio * 0.1),
            DecayTime = Math.Max(0.5, 3.0 - nextRatio * 0.3)
        });
        OnPropertyChanged(nameof(ModeCount));
        StatusMessage = $"Added mode {Modes.Count}";
    }

    /// <summary>
    /// Removes the last resonant mode.
    /// </summary>
    public void RemoveLastMode()
    {
        if (Modes.Count > 1)
        {
            Modes.RemoveAt(Modes.Count - 1);
            OnPropertyChanged(nameof(ModeCount));
            StatusMessage = $"Removed mode, {Modes.Count} remaining";
        }
    }

    /// <summary>
    /// Resets modes to default steel configuration.
    /// </summary>
    public void ResetModes()
    {
        Modes.Clear();
        Modes.Add(new ResonantModeViewModel { Index = 1, FrequencyRatio = 1.0, Amplitude = 1.0, DecayTime = 3.0 });
        Modes.Add(new ResonantModeViewModel { Index = 2, FrequencyRatio = 2.0, Amplitude = 0.6, DecayTime = 2.5 });
        Modes.Add(new ResonantModeViewModel { Index = 3, FrequencyRatio = 3.0, Amplitude = 0.4, DecayTime = 2.0 });
        Modes.Add(new ResonantModeViewModel { Index = 4, FrequencyRatio = 4.0, Amplitude = 0.3, DecayTime = 1.8 });
        Modes.Add(new ResonantModeViewModel { Index = 5, FrequencyRatio = 5.0, Amplitude = 0.2, DecayTime = 1.5 });
        Modes.Add(new ResonantModeViewModel { Index = 6, FrequencyRatio = 6.0, Amplitude = 0.15, DecayTime = 1.3 });
        OnPropertyChanged(nameof(ModeCount));
        StatusMessage = "Reset to default modes";
    }

    /// <summary>
    /// Loads a preset configuration.
    /// </summary>
    public void LoadPreset(string presetName)
    {
        switch (presetName)
        {
            case "ChurchBell":
                MaterialIndex = 5; // Bronze
                ExciterIndex = 0; // Impulse
                DecayScale = 1.5f;
                Stiffness = 0.1f;
                LoadBronzeBellModes();
                break;

            case "TubularBell":
                MaterialIndex = 0; // Steel
                ExciterIndex = 1; // Noise burst
                ExciterDecay = 2.0f;
                DecayScale = 1.2f;
                ResetModes();
                break;

            case "Vibraphone":
                MaterialIndex = 1; // Aluminum
                ExciterIndex = 1; // Noise burst
                ExciterDecay = 1.5f;
                Brightness = 0.6f;
                LoadAluminumModes();
                break;

            case "Marimba":
                MaterialIndex = 3; // Wood
                ExciterIndex = 1; // Noise burst
                ExciterDecay = 0.5f;
                Brightness = 0.35f;
                LoadWoodModes();
                break;

            case "Glockenspiel":
                MaterialIndex = 0; // Steel
                ExciterIndex = 0; // Impulse
                Brightness = 0.9f;
                DecayScale = 0.8f;
                ResetModes();
                break;

            case "BowedGlass":
                MaterialIndex = 2; // Glass
                ExciterIndex = 2; // Bow
                ExciterLevel = 0.3f;
                DecayScale = 2.0f;
                LoadGlassModes();
                break;

            case "SteelBar":
                MaterialIndex = 0; // Steel
                ExciterIndex = 0; // Impulse
                Stiffness = 0.1f;
                Brightness = 0.6f;
                ResetModes();
                break;

            case "WoodBlock":
                MaterialIndex = 3; // Wood
                ExciterIndex = 0; // Impulse
                DecayScale = 0.3f;
                LoadWoodModes();
                break;

            case "BronzeBell":
                MaterialIndex = 5; // Bronze
                ExciterIndex = 0; // Impulse
                Stiffness = 0.15f;
                Brightness = 0.5f;
                LoadBronzeBellModes();
                break;

            case "GlassChime":
                MaterialIndex = 2; // Glass
                ExciterIndex = 0; // Impulse
                Stiffness = 0.3f;
                Brightness = 0.9f;
                LoadGlassModes();
                break;
        }

        UpdateMaterialName();
        StatusMessage = $"Loaded preset: {presetName}";
    }

    private void LoadBronzeBellModes()
    {
        Modes.Clear();
        Modes.Add(new ResonantModeViewModel { Index = 1, FrequencyRatio = 0.5, Amplitude = 0.5, DecayTime = 4.0 });
        Modes.Add(new ResonantModeViewModel { Index = 2, FrequencyRatio = 1.0, Amplitude = 1.0, DecayTime = 5.0 });
        Modes.Add(new ResonantModeViewModel { Index = 3, FrequencyRatio = 1.183, Amplitude = 0.8, DecayTime = 4.0 });
        Modes.Add(new ResonantModeViewModel { Index = 4, FrequencyRatio = 1.506, Amplitude = 0.7, DecayTime = 3.5 });
        Modes.Add(new ResonantModeViewModel { Index = 5, FrequencyRatio = 2.0, Amplitude = 0.9, DecayTime = 4.5 });
        Modes.Add(new ResonantModeViewModel { Index = 6, FrequencyRatio = 2.514, Amplitude = 0.5, DecayTime = 3.0 });
        Modes.Add(new ResonantModeViewModel { Index = 7, FrequencyRatio = 3.011, Amplitude = 0.35, DecayTime = 2.5 });
        OnPropertyChanged(nameof(ModeCount));
    }

    private void LoadAluminumModes()
    {
        Modes.Clear();
        Modes.Add(new ResonantModeViewModel { Index = 1, FrequencyRatio = 1.0, Amplitude = 1.0, DecayTime = 2.0 });
        Modes.Add(new ResonantModeViewModel { Index = 2, FrequencyRatio = 2.756, Amplitude = 0.5, DecayTime = 1.5 });
        Modes.Add(new ResonantModeViewModel { Index = 3, FrequencyRatio = 5.404, Amplitude = 0.3, DecayTime = 1.2 });
        Modes.Add(new ResonantModeViewModel { Index = 4, FrequencyRatio = 8.933, Amplitude = 0.2, DecayTime = 1.0 });
        OnPropertyChanged(nameof(ModeCount));
    }

    private void LoadWoodModes()
    {
        Modes.Clear();
        Modes.Add(new ResonantModeViewModel { Index = 1, FrequencyRatio = 1.0, Amplitude = 1.0, DecayTime = 0.5 });
        Modes.Add(new ResonantModeViewModel { Index = 2, FrequencyRatio = 2.0, Amplitude = 0.4, DecayTime = 0.3 });
        Modes.Add(new ResonantModeViewModel { Index = 3, FrequencyRatio = 3.0, Amplitude = 0.2, DecayTime = 0.2 });
        Modes.Add(new ResonantModeViewModel { Index = 4, FrequencyRatio = 4.5, Amplitude = 0.1, DecayTime = 0.15 });
        OnPropertyChanged(nameof(ModeCount));
    }

    private void LoadGlassModes()
    {
        Modes.Clear();
        Modes.Add(new ResonantModeViewModel { Index = 1, FrequencyRatio = 1.0, Amplitude = 1.0, DecayTime = 4.0 });
        Modes.Add(new ResonantModeViewModel { Index = 2, FrequencyRatio = 2.32, Amplitude = 0.7, DecayTime = 3.5 });
        Modes.Add(new ResonantModeViewModel { Index = 3, FrequencyRatio = 3.88, Amplitude = 0.5, DecayTime = 3.0 });
        Modes.Add(new ResonantModeViewModel { Index = 4, FrequencyRatio = 5.59, Amplitude = 0.35, DecayTime = 2.5 });
        Modes.Add(new ResonantModeViewModel { Index = 5, FrequencyRatio = 7.44, Amplitude = 0.25, DecayTime = 2.0 });
        Modes.Add(new ResonantModeViewModel { Index = 6, FrequencyRatio = 9.44, Amplitude = 0.15, DecayTime = 1.5 });
        OnPropertyChanged(nameof(ModeCount));
    }

    private void UpdateMaterialName()
    {
        MaterialName = MaterialIndex switch
        {
            0 => "Steel",
            1 => "Aluminum",
            2 => "Glass",
            3 => "Wood",
            4 => "Brass",
            5 => "Bronze",
            6 => "Ceramic",
            7 => "Custom",
            _ => "Unknown"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

/// <summary>
/// ViewModel for a single resonant mode.
/// </summary>
public class ResonantModeViewModel : INotifyPropertyChanged
{
    private int _index;
    private double _frequencyRatio = 1.0;
    private double _amplitude = 1.0;
    private double _decayTime = 2.0;

    /// <summary>
    /// Mode index (1-based for display).
    /// </summary>
    public int Index
    {
        get => _index;
        set { _index = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Frequency ratio relative to fundamental.
    /// </summary>
    public double FrequencyRatio
    {
        get => _frequencyRatio;
        set { _frequencyRatio = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Amplitude of this mode (0-1).
    /// </summary>
    public double Amplitude
    {
        get => _amplitude;
        set { _amplitude = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Decay time in seconds.
    /// </summary>
    public double DecayTime
    {
        get => _decayTime;
        set { _decayTime = value; OnPropertyChanged(); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

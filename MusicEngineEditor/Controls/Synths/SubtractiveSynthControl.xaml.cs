// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Subtractive Synthesizer control.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using MusicEngine.Core;
using MusicEngine.Core.Synthesizers;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for SubtractiveSynthControl.xaml.
/// Provides a visual editor for classic subtractive synthesis with
/// 2 oscillators, multi-mode filter, and ADSR envelopes.
/// </summary>
public partial class SubtractiveSynthControl : UserControl
{
    private SubtractiveSynth? _synth;

    /// <summary>
    /// Creates a new SubtractiveSynthControl.
    /// </summary>
    public SubtractiveSynthControl()
    {
        InitializeComponent();
        DataContext = new SubtractiveSynthViewModel();
    }

    /// <summary>
    /// Gets or sets the subtractive synth instance being edited.
    /// </summary>
    public SubtractiveSynth? Synth
    {
        get => _synth;
        set
        {
            _synth = value;
            if (_synth != null && DataContext is SubtractiveSynthViewModel vm)
            {
                vm.LoadFromSynth(_synth);
            }
        }
    }

    private void LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string presetName && DataContext is SubtractiveSynthViewModel vm)
        {
            vm.LoadPreset(presetName);
            ApplyToSynth();
        }
    }

    private void ApplyToSynth()
    {
        if (_synth != null && DataContext is SubtractiveSynthViewModel vm)
        {
            vm.ApplyToSynth(_synth);
        }
    }
}

/// <summary>
/// ViewModel for subtractive synthesis parameters.
/// </summary>
public class SubtractiveSynthViewModel : INotifyPropertyChanged
{
    private float _volume = 0.5f;

    // Oscillator 1
    private int _osc1WaveformIndex = 2; // Sawtooth
    private float _osc1Detune;
    private float _osc1Level = 1.0f;

    // Oscillator 2
    private int _osc2WaveformIndex = 1; // Square
    private float _osc2Detune = 7f;
    private float _osc2Level = 0.5f;

    // Noise
    private float _noiseLevel;

    // Filter
    private int _filterModeIndex;
    private float _filterCutoff = 0.8f;
    private float _filterResonance = 0.2f;
    private float _filterEnvAmount = 0.5f;

    // Amp Envelope
    private double _ampAttack = 0.01;
    private double _ampDecay = 0.1;
    private double _ampSustain = 0.7;
    private double _ampRelease = 0.3;

    // Filter Envelope
    private double _filterAttack = 0.01;
    private double _filterDecay = 0.2;
    private double _filterSustain = 0.5;
    private double _filterRelease = 0.3;

    private string _statusMessage = "Ready";

    /// <summary>
    /// Gets or sets the master volume.
    /// </summary>
    public float Volume
    {
        get => _volume;
        set { _volume = value; OnPropertyChanged(); }
    }

    // Oscillator 1 properties
    public int Osc1WaveformIndex
    {
        get => _osc1WaveformIndex;
        set { _osc1WaveformIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(Osc1WaveformName)); }
    }

    public float Osc1Detune
    {
        get => _osc1Detune;
        set { _osc1Detune = value; OnPropertyChanged(); }
    }

    public float Osc1Level
    {
        get => _osc1Level;
        set { _osc1Level = value; OnPropertyChanged(); }
    }

    // Oscillator 2 properties
    public int Osc2WaveformIndex
    {
        get => _osc2WaveformIndex;
        set { _osc2WaveformIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(Osc2WaveformName)); }
    }

    public float Osc2Detune
    {
        get => _osc2Detune;
        set { _osc2Detune = value; OnPropertyChanged(); }
    }

    public float Osc2Level
    {
        get => _osc2Level;
        set { _osc2Level = value; OnPropertyChanged(); }
    }

    // Noise
    public float NoiseLevel
    {
        get => _noiseLevel;
        set { _noiseLevel = value; OnPropertyChanged(); }
    }

    // Filter properties
    public int FilterModeIndex
    {
        get => _filterModeIndex;
        set { _filterModeIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(FilterModeName)); }
    }

    public float FilterCutoff
    {
        get => _filterCutoff;
        set { _filterCutoff = value; OnPropertyChanged(); }
    }

    public float FilterResonance
    {
        get => _filterResonance;
        set { _filterResonance = value; OnPropertyChanged(); }
    }

    public float FilterEnvAmount
    {
        get => _filterEnvAmount;
        set { _filterEnvAmount = value; OnPropertyChanged(); }
    }

    // Amp Envelope properties
    public double AmpAttack
    {
        get => _ampAttack;
        set { _ampAttack = value; OnPropertyChanged(); }
    }

    public double AmpDecay
    {
        get => _ampDecay;
        set { _ampDecay = value; OnPropertyChanged(); }
    }

    public double AmpSustain
    {
        get => _ampSustain;
        set { _ampSustain = value; OnPropertyChanged(); }
    }

    public double AmpRelease
    {
        get => _ampRelease;
        set { _ampRelease = value; OnPropertyChanged(); }
    }

    // Filter Envelope properties
    public double FilterAttack
    {
        get => _filterAttack;
        set { _filterAttack = value; OnPropertyChanged(); }
    }

    public double FilterDecay
    {
        get => _filterDecay;
        set { _filterDecay = value; OnPropertyChanged(); }
    }

    public double FilterSustain
    {
        get => _filterSustain;
        set { _filterSustain = value; OnPropertyChanged(); }
    }

    public double FilterRelease
    {
        get => _filterRelease;
        set { _filterRelease = value; OnPropertyChanged(); }
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
    /// Gets the oscillator 1 waveform name.
    /// </summary>
    public string Osc1WaveformName => GetWaveformName(Osc1WaveformIndex);

    /// <summary>
    /// Gets the oscillator 2 waveform name.
    /// </summary>
    public string Osc2WaveformName => GetWaveformName(Osc2WaveformIndex);

    /// <summary>
    /// Gets the filter mode name.
    /// </summary>
    public string FilterModeName => FilterModeIndex switch
    {
        0 => "LP",
        1 => "HP",
        2 => "BP",
        3 => "Notch",
        _ => "LP"
    };

    /// <summary>
    /// Loads parameters from a SubtractiveSynth instance.
    /// </summary>
    public void LoadFromSynth(SubtractiveSynth synth)
    {
        Volume = synth.Volume;

        Osc1WaveformIndex = (int)synth.Osc1Waveform;
        Osc1Detune = synth.Osc1Detune;
        Osc1Level = synth.Osc1Level;

        Osc2WaveformIndex = (int)synth.Osc2Waveform;
        Osc2Detune = synth.Osc2Detune;
        Osc2Level = synth.Osc2Level;

        NoiseLevel = synth.NoiseLevel;

        FilterModeIndex = (int)synth.FilterMode;
        FilterCutoff = synth.FilterCutoff;
        FilterResonance = synth.FilterResonance;
        FilterEnvAmount = synth.FilterEnvAmount;

        AmpAttack = synth.AmpAttack;
        AmpDecay = synth.AmpDecay;
        AmpSustain = synth.AmpSustain;
        AmpRelease = synth.AmpRelease;

        FilterAttack = synth.FilterAttack;
        FilterDecay = synth.FilterDecay;
        FilterSustain = synth.FilterSustain;
        FilterRelease = synth.FilterRelease;

        StatusMessage = $"Loaded {synth.Name}";
    }

    /// <summary>
    /// Applies current parameters to a SubtractiveSynth instance.
    /// </summary>
    public void ApplyToSynth(SubtractiveSynth synth)
    {
        synth.Volume = Volume;

        synth.Osc1Waveform = (WaveType)Osc1WaveformIndex;
        synth.Osc1Detune = Osc1Detune;
        synth.Osc1Level = Osc1Level;

        synth.Osc2Waveform = (WaveType)Osc2WaveformIndex;
        synth.Osc2Detune = Osc2Detune;
        synth.Osc2Level = Osc2Level;

        synth.NoiseLevel = NoiseLevel;

        synth.FilterMode = (SubtractiveFilterMode)FilterModeIndex;
        synth.FilterCutoff = FilterCutoff;
        synth.FilterResonance = FilterResonance;
        synth.FilterEnvAmount = FilterEnvAmount;

        synth.AmpAttack = AmpAttack;
        synth.AmpDecay = AmpDecay;
        synth.AmpSustain = AmpSustain;
        synth.AmpRelease = AmpRelease;

        synth.FilterAttack = FilterAttack;
        synth.FilterDecay = FilterDecay;
        synth.FilterSustain = FilterSustain;
        synth.FilterRelease = FilterRelease;
    }

    /// <summary>
    /// Loads a preset configuration.
    /// </summary>
    public void LoadPreset(string presetName)
    {
        switch (presetName)
        {
            case "AnalogBass":
                Osc1WaveformIndex = 2; // Sawtooth
                Osc1Level = 1.0f;
                Osc1Detune = 0;
                Osc2WaveformIndex = 1; // Square
                Osc2Level = 0.5f;
                Osc2Detune = 5f;
                NoiseLevel = 0;
                FilterModeIndex = 0; // LP
                FilterCutoff = 0.3f;
                FilterResonance = 0.4f;
                FilterEnvAmount = 0.7f;
                AmpAttack = 0.001; AmpDecay = 0.1; AmpSustain = 0.8; AmpRelease = 0.1;
                FilterAttack = 0.001; FilterDecay = 0.3; FilterSustain = 0.2; FilterRelease = 0.2;
                break;

            case "WarmPad":
                Osc1WaveformIndex = 2; // Sawtooth
                Osc1Level = 0.7f;
                Osc1Detune = 0;
                Osc2WaveformIndex = 2; // Sawtooth
                Osc2Level = 0.7f;
                Osc2Detune = 12f;
                NoiseLevel = 0;
                FilterModeIndex = 0; // LP
                FilterCutoff = 0.4f;
                FilterResonance = 0.2f;
                FilterEnvAmount = 0.3f;
                AmpAttack = 0.5; AmpDecay = 0.5; AmpSustain = 0.8; AmpRelease = 1.5;
                FilterAttack = 0.5; FilterDecay = 1.0; FilterSustain = 0.6; FilterRelease = 1.5;
                break;

            case "SyncLead":
                Osc1WaveformIndex = 2; // Sawtooth
                Osc1Level = 0.8f;
                Osc1Detune = 0;
                Osc2WaveformIndex = 2; // Sawtooth
                Osc2Level = 0.8f;
                Osc2Detune = 0;
                NoiseLevel = 0;
                FilterModeIndex = 0; // LP
                FilterCutoff = 0.6f;
                FilterResonance = 0.4f;
                FilterEnvAmount = 0.5f;
                AmpAttack = 0.01; AmpDecay = 0.2; AmpSustain = 0.7; AmpRelease = 0.3;
                FilterAttack = 0.01; FilterDecay = 0.3; FilterSustain = 0.4; FilterRelease = 0.3;
                break;

            case "PWMStrings":
                Osc1WaveformIndex = 1; // Square
                Osc1Level = 0.6f;
                Osc1Detune = 0;
                Osc2WaveformIndex = 1; // Square
                Osc2Level = 0.6f;
                Osc2Detune = 10f;
                NoiseLevel = 0;
                FilterModeIndex = 0; // LP
                FilterCutoff = 0.5f;
                FilterResonance = 0.1f;
                FilterEnvAmount = 0.2f;
                AmpAttack = 0.3; AmpDecay = 0.3; AmpSustain = 0.9; AmpRelease = 0.5;
                FilterAttack = 0.3; FilterDecay = 0.5; FilterSustain = 0.7; FilterRelease = 0.8;
                break;

            case "AutoPanArp":
                Osc1WaveformIndex = 2; // Sawtooth
                Osc1Level = 0.8f;
                Osc1Detune = 0;
                Osc2WaveformIndex = 1; // Square
                Osc2Level = 0.4f;
                Osc2Detune = 0;
                NoiseLevel = 0;
                FilterModeIndex = 0; // LP
                FilterCutoff = 0.6f;
                FilterResonance = 0.3f;
                FilterEnvAmount = 0.4f;
                AmpAttack = 0.01; AmpDecay = 0.15; AmpSustain = 0.5; AmpRelease = 0.2;
                FilterAttack = 0.01; FilterDecay = 0.2; FilterSustain = 0.3; FilterRelease = 0.2;
                break;

            case "ResonantPluck":
                Osc1WaveformIndex = 2; // Sawtooth
                Osc1Level = 1.0f;
                Osc1Detune = 0;
                Osc2WaveformIndex = 3; // Triangle
                Osc2Level = 0.3f;
                Osc2Detune = 3f;
                NoiseLevel = 0;
                FilterModeIndex = 0; // LP
                FilterCutoff = 0.2f;
                FilterResonance = 0.7f;
                FilterEnvAmount = 0.8f;
                AmpAttack = 0.001; AmpDecay = 0.4; AmpSustain = 0; AmpRelease = 0.3;
                FilterAttack = 0.001; FilterDecay = 0.4; FilterSustain = 0; FilterRelease = 0.3;
                break;

            case "FatSaw":
                Osc1WaveformIndex = 2; // Sawtooth
                Osc1Level = 0.9f;
                Osc1Detune = -5;
                Osc2WaveformIndex = 2; // Sawtooth
                Osc2Level = 0.9f;
                Osc2Detune = 5;
                NoiseLevel = 0;
                FilterModeIndex = 0; // LP
                FilterCutoff = 0.7f;
                FilterResonance = 0.15f;
                FilterEnvAmount = 0.2f;
                AmpAttack = 0.02; AmpDecay = 0.2; AmpSustain = 0.8; AmpRelease = 0.4;
                FilterAttack = 0.02; FilterDecay = 0.3; FilterSustain = 0.5; FilterRelease = 0.4;
                break;

            case "SoftLead":
                Osc1WaveformIndex = 0; // Sine
                Osc1Level = 0.8f;
                Osc1Detune = 0;
                Osc2WaveformIndex = 3; // Triangle
                Osc2Level = 0.5f;
                Osc2Detune = 0;
                NoiseLevel = 0;
                FilterModeIndex = 0; // LP
                FilterCutoff = 0.6f;
                FilterResonance = 0.1f;
                FilterEnvAmount = 0.3f;
                AmpAttack = 0.05; AmpDecay = 0.2; AmpSustain = 0.7; AmpRelease = 0.3;
                FilterAttack = 0.05; FilterDecay = 0.3; FilterSustain = 0.5; FilterRelease = 0.3;
                break;

            case "SubBass":
                Osc1WaveformIndex = 0; // Sine
                Osc1Level = 1.0f;
                Osc1Detune = 0;
                Osc2WaveformIndex = 1; // Square
                Osc2Level = 0.3f;
                Osc2Detune = 0;
                NoiseLevel = 0;
                FilterModeIndex = 0; // LP
                FilterCutoff = 0.15f;
                FilterResonance = 0.2f;
                FilterEnvAmount = 0.4f;
                AmpAttack = 0.01; AmpDecay = 0.1; AmpSustain = 0.9; AmpRelease = 0.15;
                FilterAttack = 0.01; FilterDecay = 0.2; FilterSustain = 0.3; FilterRelease = 0.15;
                break;

            case "BrightLead":
                Osc1WaveformIndex = 2; // Sawtooth
                Osc1Level = 1.0f;
                Osc1Detune = 0;
                Osc2WaveformIndex = 2; // Sawtooth
                Osc2Level = 0.5f;
                Osc2Detune = 7;
                NoiseLevel = 0;
                FilterModeIndex = 0; // LP
                FilterCutoff = 0.85f;
                FilterResonance = 0.25f;
                FilterEnvAmount = 0.2f;
                AmpAttack = 0.01; AmpDecay = 0.15; AmpSustain = 0.7; AmpRelease = 0.25;
                FilterAttack = 0.01; FilterDecay = 0.2; FilterSustain = 0.6; FilterRelease = 0.25;
                break;
        }

        StatusMessage = $"Loaded preset: {presetName}";
    }

    private static string GetWaveformName(int index)
    {
        return index switch
        {
            0 => "Sine",
            1 => "Square",
            2 => "Saw",
            3 => "Triangle",
            4 => "Noise",
            _ => "Unknown"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

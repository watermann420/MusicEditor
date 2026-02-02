// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the Wavefolder Synthesizer control.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using MusicEngine.Core.Synthesizers;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for WavefolderSynthControl.xaml.
/// Provides a visual editor for wave folding synthesis with multiple algorithms.
/// </summary>
public partial class WavefolderSynthControl : UserControl
{
    private WavefolderSynth? _synth;

    /// <summary>
    /// Creates a new WavefolderSynthControl.
    /// </summary>
    public WavefolderSynthControl()
    {
        InitializeComponent();
        DataContext = new WavefolderSynthViewModel();
    }

    /// <summary>
    /// Gets or sets the wavefolder synth instance being edited.
    /// </summary>
    public WavefolderSynth? Synth
    {
        get => _synth;
        set
        {
            _synth = value;
            if (_synth != null && DataContext is WavefolderSynthViewModel vm)
            {
                vm.LoadFromSynth(_synth);
            }
        }
    }

    private void LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string presetName && DataContext is WavefolderSynthViewModel vm)
        {
            vm.LoadPreset(presetName);
            ApplyToSynth();
        }
    }

    private void ApplyToSynth()
    {
        if (_synth != null && DataContext is WavefolderSynthViewModel vm)
        {
            vm.ApplyToSynth(_synth);
        }
    }
}

/// <summary>
/// ViewModel for wavefolder synthesis parameters.
/// </summary>
public class WavefolderSynthViewModel : INotifyPropertyChanged
{
    private float _volume = 0.5f;
    private int _oscillatorTypeIndex;
    private float _drive = 0.5f;
    private float _detune;
    private float _pulseWidth = 0.5f;
    private float _foldAmount = 0.5f;
    private float _symmetry = 0.5f;
    private int _foldStages = 3;
    private int _algorithmIndex;
    private float _mix = 1.0f;
    private float _foldEnvelopeDepth;

    // Envelope
    private double _attack = 0.01;
    private double _decay = 0.1;
    private double _sustain = 0.8;
    private double _release = 0.3;

    private string _statusMessage = "Ready";

    /// <summary>
    /// Gets or sets the master volume.
    /// </summary>
    public float Volume
    {
        get => _volume;
        set { _volume = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the oscillator type index.
    /// </summary>
    public int OscillatorTypeIndex
    {
        get => _oscillatorTypeIndex;
        set { _oscillatorTypeIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(OscillatorName)); }
    }

    /// <summary>
    /// Gets or sets the input drive/gain.
    /// </summary>
    public float Drive
    {
        get => _drive;
        set { _drive = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the detune in cents.
    /// </summary>
    public float Detune
    {
        get => _detune;
        set { _detune = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the pulse width.
    /// </summary>
    public float PulseWidth
    {
        get => _pulseWidth;
        set { _pulseWidth = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the fold amount.
    /// </summary>
    public float FoldAmount
    {
        get => _foldAmount;
        set { _foldAmount = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the symmetry.
    /// </summary>
    public float Symmetry
    {
        get => _symmetry;
        set { _symmetry = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the number of fold stages.
    /// </summary>
    public int FoldStages
    {
        get => _foldStages;
        set { _foldStages = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the algorithm index.
    /// </summary>
    public int AlgorithmIndex
    {
        get => _algorithmIndex;
        set { _algorithmIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(AlgorithmName)); }
    }

    /// <summary>
    /// Gets or sets the dry/wet mix.
    /// </summary>
    public float Mix
    {
        get => _mix;
        set { _mix = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets the fold envelope depth.
    /// </summary>
    public float FoldEnvelopeDepth
    {
        get => _foldEnvelopeDepth;
        set { _foldEnvelopeDepth = value; OnPropertyChanged(); }
    }

    // Envelope properties
    public double Attack
    {
        get => _attack;
        set { _attack = value; OnPropertyChanged(); }
    }

    public double Decay
    {
        get => _decay;
        set { _decay = value; OnPropertyChanged(); }
    }

    public double Sustain
    {
        get => _sustain;
        set { _sustain = value; OnPropertyChanged(); }
    }

    public double Release
    {
        get => _release;
        set { _release = value; OnPropertyChanged(); }
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
    /// Gets the oscillator name.
    /// </summary>
    public string OscillatorName => OscillatorTypeIndex switch
    {
        0 => "Sine",
        1 => "Sawtooth",
        2 => "Triangle",
        3 => "Square",
        _ => "Unknown"
    };

    /// <summary>
    /// Gets the algorithm name.
    /// </summary>
    public string AlgorithmName => AlgorithmIndex switch
    {
        0 => "Sine",
        1 => "Triangle",
        2 => "Soft Clip",
        3 => "Hard",
        4 => "Asymmetric",
        5 => "Multi-Stage",
        6 => "Serge",
        _ => "Unknown"
    };

    /// <summary>
    /// Loads parameters from a WavefolderSynth instance.
    /// </summary>
    public void LoadFromSynth(WavefolderSynth synth)
    {
        Volume = synth.Volume;
        OscillatorTypeIndex = (int)synth.OscillatorType;
        Drive = synth.Drive;
        Detune = synth.Detune;
        PulseWidth = synth.PulseWidth;
        FoldAmount = synth.FoldAmount;
        Symmetry = synth.Symmetry;
        FoldStages = synth.FoldStages;
        AlgorithmIndex = (int)synth.Algorithm;
        Mix = synth.Mix;
        FoldEnvelopeDepth = synth.FoldEnvelopeDepth;

        Attack = synth.AmpEnvelope.Attack;
        Decay = synth.AmpEnvelope.Decay;
        Sustain = synth.AmpEnvelope.Sustain;
        Release = synth.AmpEnvelope.Release;

        StatusMessage = $"Loaded {synth.Name}";
    }

    /// <summary>
    /// Applies current parameters to a WavefolderSynth instance.
    /// </summary>
    public void ApplyToSynth(WavefolderSynth synth)
    {
        synth.Volume = Volume;
        synth.OscillatorType = (WavefolderOscillatorType)OscillatorTypeIndex;
        synth.Drive = Drive;
        synth.Detune = Detune;
        synth.PulseWidth = PulseWidth;
        synth.FoldAmount = FoldAmount;
        synth.Symmetry = Symmetry;
        synth.FoldStages = FoldStages;
        synth.Algorithm = (WavefoldingAlgorithm)AlgorithmIndex;
        synth.Mix = Mix;
        synth.FoldEnvelopeDepth = FoldEnvelopeDepth;

        synth.AmpEnvelope.Attack = Attack;
        synth.AmpEnvelope.Decay = Decay;
        synth.AmpEnvelope.Sustain = Sustain;
        synth.AmpEnvelope.Release = Release;
    }

    /// <summary>
    /// Loads a preset configuration.
    /// </summary>
    public void LoadPreset(string presetName)
    {
        switch (presetName)
        {
            case "Smooth":
                OscillatorTypeIndex = 0; // Sine
                AlgorithmIndex = 0; // Sine
                FoldAmount = 0.4f;
                Symmetry = 0.5f;
                Drive = 0.3f;
                Mix = 0.8f;
                FoldStages = 3;
                Attack = 0.02; Decay = 0.2; Sustain = 0.7; Release = 0.4;
                break;

            case "Aggressive":
                OscillatorTypeIndex = 1; // Saw
                AlgorithmIndex = 3; // Hard
                FoldAmount = 0.7f;
                Symmetry = 0.5f;
                Drive = 0.6f;
                Mix = 1.0f;
                FoldStages = 3;
                Attack = 0.001; Decay = 0.15; Sustain = 0.6; Release = 0.2;
                break;

            case "Serge":
                OscillatorTypeIndex = 2; // Triangle
                AlgorithmIndex = 6; // Serge
                FoldAmount = 0.5f;
                Symmetry = 0.5f;
                Drive = 0.5f;
                Mix = 1.0f;
                FoldStages = 3;
                Attack = 0.01; Decay = 0.3; Sustain = 0.8; Release = 0.5;
                break;

            case "Warm":
                OscillatorTypeIndex = 0; // Sine
                AlgorithmIndex = 2; // SoftClip
                FoldAmount = 0.35f;
                Symmetry = 0.4f;
                Drive = 0.4f;
                Mix = 0.7f;
                FoldStages = 2;
                Attack = 0.05; Decay = 0.25; Sustain = 0.75; Release = 0.6;
                break;

            case "Complex":
                OscillatorTypeIndex = 1; // Saw
                AlgorithmIndex = 5; // MultiStage
                FoldAmount = 0.6f;
                Symmetry = 0.5f;
                Drive = 0.5f;
                Mix = 0.9f;
                FoldStages = 4;
                Attack = 0.01; Decay = 0.4; Sustain = 0.7; Release = 0.5;
                break;

            case "Buzzy":
                OscillatorTypeIndex = 3; // Square
                AlgorithmIndex = 1; // Triangle
                FoldAmount = 0.65f;
                Symmetry = 0.5f;
                Drive = 0.55f;
                Mix = 1.0f;
                FoldStages = 3;
                Attack = 0.001; Decay = 0.1; Sustain = 0.7; Release = 0.15;
                break;

            case "Subtle":
                OscillatorTypeIndex = 0; // Sine
                AlgorithmIndex = 0; // Sine
                FoldAmount = 0.2f;
                Symmetry = 0.5f;
                Drive = 0.2f;
                Mix = 0.5f;
                FoldStages = 1;
                Attack = 0.05; Decay = 0.3; Sustain = 0.8; Release = 0.5;
                break;

            case "Heavy":
                OscillatorTypeIndex = 1; // Saw
                AlgorithmIndex = 3; // Hard
                FoldAmount = 0.9f;
                Symmetry = 0.5f;
                Drive = 0.8f;
                Mix = 1.0f;
                FoldStages = 5;
                Attack = 0.001; Decay = 0.1; Sustain = 0.8; Release = 0.2;
                break;

            case "Asymmetric":
                OscillatorTypeIndex = 2; // Triangle
                AlgorithmIndex = 4; // Asymmetric
                FoldAmount = 0.5f;
                Symmetry = 0.3f;
                Drive = 0.45f;
                Mix = 1.0f;
                FoldStages = 3;
                Attack = 0.02; Decay = 0.2; Sustain = 0.7; Release = 0.4;
                break;

            case "Buchla":
                OscillatorTypeIndex = 0; // Sine
                AlgorithmIndex = 5; // MultiStage
                FoldAmount = 0.55f;
                Symmetry = 0.5f;
                Drive = 0.5f;
                Mix = 1.0f;
                FoldStages = 3;
                FoldEnvelopeDepth = 0.4f;
                Attack = 0.01; Decay = 0.3; Sustain = 0.6; Release = 0.5;
                break;
        }

        StatusMessage = $"Loaded preset: {presetName}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

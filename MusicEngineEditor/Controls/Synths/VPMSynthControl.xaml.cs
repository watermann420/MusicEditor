// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the VPM (Phase Modulation) Synthesizer control.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using MusicEngine.Core.Synthesizers;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for VPMSynthControl.xaml.
/// Provides a visual editor for Casio CZ style phase distortion synthesis.
/// </summary>
public partial class VPMSynthControl : UserControl
{
    private VPMSynth? _synth;

    /// <summary>
    /// Creates a new VPMSynthControl.
    /// </summary>
    public VPMSynthControl()
    {
        InitializeComponent();
        DataContext = new VPMSynthViewModel();
    }

    /// <summary>
    /// Gets or sets the VPM synth instance being edited.
    /// </summary>
    public VPMSynth? Synth
    {
        get => _synth;
        set
        {
            _synth = value;
            if (_synth != null && DataContext is VPMSynthViewModel vm)
            {
                vm.LoadFromSynth(_synth);
            }
        }
    }

    private void LoadPreset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string presetName && DataContext is VPMSynthViewModel vm)
        {
            vm.LoadPreset(presetName);
            ApplyToSynth();
        }
    }

    private void ApplyToSynth()
    {
        if (_synth != null && DataContext is VPMSynthViewModel vm)
        {
            vm.ApplyToSynth(_synth);
        }
    }
}

/// <summary>
/// ViewModel for VPM synthesis parameters.
/// </summary>
public class VPMSynthViewModel : INotifyPropertyChanged
{
    private float _volume = 0.5f;
    private int _waveform1Index;
    private int _waveform2Index;
    private bool _line2Enable;
    private float _line2Level = 0.5f;
    private float _detune;
    private int _lineModeIndex;
    private float _dcoDepth;

    // DCW Envelope
    private int _dcwAttack = 90;
    private int _dcwDecay = 40;
    private int _dcwSustain = 50;
    private int _dcwRelease = 30;

    // DCA Envelope
    private int _dcaAttack = 80;
    private int _dcaDecay = 50;
    private int _dcaSustain = 70;
    private int _dcaRelease = 40;

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
    /// Gets or sets the waveform 1 index.
    /// </summary>
    public int Waveform1Index
    {
        get => _waveform1Index;
        set { _waveform1Index = value; OnPropertyChanged(); OnPropertyChanged(nameof(Waveform1Name)); }
    }

    /// <summary>
    /// Gets or sets the waveform 2 index.
    /// </summary>
    public int Waveform2Index
    {
        get => _waveform2Index;
        set { _waveform2Index = value; OnPropertyChanged(); }
    }

    /// <summary>
    /// Gets or sets whether line 2 is enabled.
    /// </summary>
    public bool Line2Enable
    {
        get => _line2Enable;
        set { _line2Enable = value; OnPropertyChanged(); OnPropertyChanged(nameof(Line2StatusText)); }
    }

    /// <summary>
    /// Gets or sets the line 2 level.
    /// </summary>
    public float Line2Level
    {
        get => _line2Level;
        set { _line2Level = value; OnPropertyChanged(); }
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
    /// Gets or sets the line mode index.
    /// </summary>
    public int LineModeIndex
    {
        get => _lineModeIndex;
        set { _lineModeIndex = value; OnPropertyChanged(); OnPropertyChanged(nameof(Line2StatusText)); }
    }

    /// <summary>
    /// Gets or sets the DCO (pitch) envelope depth.
    /// </summary>
    public float DcoDepth
    {
        get => _dcoDepth;
        set { _dcoDepth = value; OnPropertyChanged(); }
    }

    // DCW Envelope properties
    public int DcwAttack
    {
        get => _dcwAttack;
        set { _dcwAttack = value; OnPropertyChanged(); }
    }

    public int DcwDecay
    {
        get => _dcwDecay;
        set { _dcwDecay = value; OnPropertyChanged(); }
    }

    public int DcwSustain
    {
        get => _dcwSustain;
        set { _dcwSustain = value; OnPropertyChanged(); }
    }

    public int DcwRelease
    {
        get => _dcwRelease;
        set { _dcwRelease = value; OnPropertyChanged(); }
    }

    // DCA Envelope properties
    public int DcaAttack
    {
        get => _dcaAttack;
        set { _dcaAttack = value; OnPropertyChanged(); }
    }

    public int DcaDecay
    {
        get => _dcaDecay;
        set { _dcaDecay = value; OnPropertyChanged(); }
    }

    public int DcaSustain
    {
        get => _dcaSustain;
        set { _dcaSustain = value; OnPropertyChanged(); }
    }

    public int DcaRelease
    {
        get => _dcaRelease;
        set { _dcaRelease = value; OnPropertyChanged(); }
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
    /// Gets the waveform 1 name.
    /// </summary>
    public string Waveform1Name => GetWaveformName(Waveform1Index);

    /// <summary>
    /// Gets the line 2 status text.
    /// </summary>
    public string Line2StatusText
    {
        get
        {
            if (!Line2Enable) return "Off";
            return LineModeIndex switch
            {
                0 => "Mix",
                1 => "Ring",
                2 => "Sync",
                _ => "On"
            };
        }
    }

    /// <summary>
    /// Loads parameters from a VPMSynth instance.
    /// </summary>
    public void LoadFromSynth(VPMSynth synth)
    {
        Volume = synth.Volume;
        Waveform1Index = (int)synth.Waveform1;
        Waveform2Index = (int)synth.Waveform2;
        Line2Enable = synth.Line2Enable;
        Line2Level = synth.Line2Level;
        Detune = synth.Detune;
        LineModeIndex = (int)synth.LineMode;
        DcoDepth = synth.DcoDepth;

        // Load DCA envelope
        DcaAttack = synth.DcaEnvelope.Stages[0].Rate;
        DcaDecay = synth.DcaEnvelope.Stages[1].Rate;
        DcaSustain = synth.DcaEnvelope.Stages[1].Level;
        DcaRelease = synth.DcaEnvelope.Stages[4].Rate;

        // Load DCW envelope
        DcwAttack = synth.DcwEnvelope.Stages[0].Rate;
        DcwDecay = synth.DcwEnvelope.Stages[1].Rate;
        DcwSustain = synth.DcwEnvelope.Stages[1].Level;
        DcwRelease = synth.DcwEnvelope.Stages[4].Rate;

        StatusMessage = $"Loaded {synth.Name}";
    }

    /// <summary>
    /// Applies current parameters to a VPMSynth instance.
    /// </summary>
    public void ApplyToSynth(VPMSynth synth)
    {
        synth.Volume = Volume;
        synth.Waveform1 = (VPMWaveform)Waveform1Index;
        synth.Waveform2 = (VPMWaveform)Waveform2Index;
        synth.Line2Enable = Line2Enable;
        synth.Line2Level = Line2Level;
        synth.Detune = Detune;
        synth.LineMode = (VPMLineMode)LineModeIndex;
        synth.DcoDepth = DcoDepth;

        // Apply DCA envelope
        synth.DcaEnvelope.SetADSR(DcaAttack, DcaDecay, DcaSustain, DcaRelease);

        // Apply DCW envelope
        synth.DcwEnvelope.SetADSR(DcwAttack, DcwDecay, DcwSustain, DcwRelease);
    }

    /// <summary>
    /// Loads a preset configuration.
    /// </summary>
    public void LoadPreset(string presetName)
    {
        switch (presetName)
        {
            case "CZBass":
                Waveform1Index = 5; // Resonant1
                Line2Enable = false;
                DcaAttack = 95; DcaDecay = 60; DcaSustain = 50; DcaRelease = 50;
                DcwAttack = 99; DcwDecay = 40; DcwSustain = 20; DcwRelease = 30;
                DcoDepth = 0;
                break;

            case "CZOrgan":
                Waveform1Index = 1; // Square
                Waveform2Index = 1; // Square
                Line2Enable = true;
                Line2Level = 0.5f;
                LineModeIndex = 0; // Mix
                Detune = 0;
                DcaAttack = 99; DcaDecay = 99; DcaSustain = 99; DcaRelease = 60;
                DcwAttack = 99; DcwDecay = 99; DcwSustain = 80; DcwRelease = 50;
                DcoDepth = 0;
                break;

            case "CZStrings":
                Waveform1Index = 0; // Sawtooth
                Waveform2Index = 0; // Sawtooth
                Line2Enable = true;
                Line2Level = 0.7f;
                LineModeIndex = 0; // Mix
                Detune = 8;
                DcaAttack = 30; DcaDecay = 50; DcaSustain = 80; DcaRelease = 40;
                DcwAttack = 20; DcwDecay = 40; DcwSustain = 60; DcwRelease = 30;
                DcoDepth = 0;
                break;

            case "CZBells":
                Waveform1Index = 6; // Resonant2
                Waveform2Index = 3; // DoubleSine
                Line2Enable = true;
                Line2Level = 0.4f;
                LineModeIndex = 1; // RingMod
                DcaAttack = 99; DcaDecay = 30; DcaSustain = 0; DcaRelease = 20;
                DcwAttack = 99; DcwDecay = 20; DcwSustain = 30; DcwRelease = 15;
                DcoDepth = 0;
                break;

            case "CZSyncLead":
                Waveform1Index = 4; // SawPulse
                Waveform2Index = 0; // Sawtooth
                Line2Enable = true;
                Line2Level = 0.6f;
                LineModeIndex = 2; // Sync
                Detune = 700;
                DcaAttack = 90; DcaDecay = 50; DcaSustain = 70; DcaRelease = 40;
                DcwAttack = 70; DcwDecay = 40; DcwSustain = 40; DcwRelease = 30;
                DcoDepth = 0.5f;
                break;

            case "ResonantPad":
                Waveform1Index = 5; // Resonant1
                Waveform2Index = 6; // Resonant2
                Line2Enable = true;
                Line2Level = 0.5f;
                LineModeIndex = 0; // Mix
                Detune = 5;
                DcaAttack = 20; DcaDecay = 40; DcaSustain = 80; DcaRelease = 50;
                DcwAttack = 30; DcwDecay = 50; DcwSustain = 70; DcwRelease = 40;
                DcoDepth = 0;
                break;

            case "PhaseLead":
                Waveform1Index = 0; // Sawtooth
                Line2Enable = false;
                DcaAttack = 90; DcaDecay = 40; DcaSustain = 60; DcaRelease = 30;
                DcwAttack = 80; DcwDecay = 30; DcwSustain = 40; DcwRelease = 25;
                DcoDepth = 0.2f;
                break;

            case "DigitalEP":
                Waveform1Index = 7; // Resonant3
                Waveform2Index = 3; // DoubleSine
                Line2Enable = true;
                Line2Level = 0.3f;
                LineModeIndex = 0; // Mix
                Detune = 0;
                DcaAttack = 99; DcaDecay = 40; DcaSustain = 30; DcaRelease = 35;
                DcwAttack = 99; DcwDecay = 30; DcwSustain = 20; DcwRelease = 25;
                DcoDepth = 0;
                break;

            case "PulseBass":
                Waveform1Index = 2; // Pulse
                Line2Enable = false;
                DcaAttack = 99; DcaDecay = 50; DcaSustain = 60; DcaRelease = 40;
                DcwAttack = 99; DcwDecay = 35; DcwSustain = 30; DcwRelease = 25;
                DcoDepth = 0;
                break;

            case "RingMod":
                Waveform1Index = 0; // Sawtooth
                Waveform2Index = 1; // Square
                Line2Enable = true;
                Line2Level = 0.8f;
                LineModeIndex = 1; // RingMod
                Detune = 0;
                DcaAttack = 90; DcaDecay = 50; DcaSustain = 70; DcaRelease = 40;
                DcwAttack = 90; DcwDecay = 40; DcwSustain = 50; DcwRelease = 30;
                DcoDepth = 0;
                break;
        }

        StatusMessage = $"Loaded preset: {presetName}";
    }

    private static string GetWaveformName(int index)
    {
        return index switch
        {
            0 => "Sawtooth",
            1 => "Square",
            2 => "Pulse",
            3 => "Double Sine",
            4 => "Saw-Pulse",
            5 => "Resonant 1",
            6 => "Resonant 2",
            7 => "Resonant 3",
            _ => "Unknown"
        };
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

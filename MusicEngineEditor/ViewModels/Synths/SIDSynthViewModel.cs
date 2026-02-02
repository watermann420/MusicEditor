// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the SID Synth Editor control.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Synthesizers;
using NAudio.Wave;

namespace MusicEngineEditor.ViewModels.Synths;

/// <summary>
/// ViewModel for the SID Synth Editor control.
/// Provides a visual editor for the MusicEngine SIDSynth (Commodore 64 SID chip emulation)
/// with 3 oscillators, filter section, and preset management.
/// </summary>
public partial class SIDSynthViewModel : ViewModelBase, IDisposable
{
    #region Private Fields

    private SIDSynth? _sidSynth;
    private WaveOutEvent? _waveOut;
    private bool _isInitialized;
    private bool _disposed;
    private readonly object _lock = new();

    #endregion

    #region Global Properties

    /// <summary>
    /// Master volume (0-1).
    /// </summary>
    [ObservableProperty]
    private float _volume = 0.5f;

    /// <summary>
    /// Chip model index (0 = 6581, 1 = 8580).
    /// </summary>
    [ObservableProperty]
    private int _chipModelIndex;

    /// <summary>
    /// Chip model display name.
    /// </summary>
    public string ChipModelName => ChipModelIndex == 0 ? "MOS 6581" : "MOS 8580";

    #endregion

    #region Oscillator 1 Properties

    /// <summary>
    /// Oscillator 1 waveform index (0=Triangle, 1=Saw, 2=Pulse, 3=Noise).
    /// </summary>
    [ObservableProperty]
    private int _osc1WaveformIndex = 2; // Pulse

    /// <summary>
    /// Oscillator 1 pulse width (0-4095).
    /// </summary>
    [ObservableProperty]
    private int _osc1PulseWidth = 2048;

    /// <summary>
    /// Oscillator 1 hard sync enabled.
    /// </summary>
    [ObservableProperty]
    private bool _osc1Sync;

    /// <summary>
    /// Oscillator 1 ring modulation enabled.
    /// </summary>
    [ObservableProperty]
    private bool _osc1RingMod;

    /// <summary>
    /// Oscillator 1 routed through filter.
    /// </summary>
    [ObservableProperty]
    private bool _osc1FilterRoute = true;

    /// <summary>
    /// Oscillator 1 attack (0-15).
    /// </summary>
    [ObservableProperty]
    private int _osc1Attack = 2;

    /// <summary>
    /// Oscillator 1 decay (0-15).
    /// </summary>
    [ObservableProperty]
    private int _osc1Decay = 4;

    /// <summary>
    /// Oscillator 1 sustain (0-15).
    /// </summary>
    [ObservableProperty]
    private int _osc1Sustain = 8;

    /// <summary>
    /// Oscillator 1 release (0-15).
    /// </summary>
    [ObservableProperty]
    private int _osc1Release = 4;

    #endregion

    #region Oscillator 2 Properties

    /// <summary>
    /// Oscillator 2 waveform index.
    /// </summary>
    [ObservableProperty]
    private int _osc2WaveformIndex = 2;

    /// <summary>
    /// Oscillator 2 pulse width (0-4095).
    /// </summary>
    [ObservableProperty]
    private int _osc2PulseWidth = 2048;

    /// <summary>
    /// Oscillator 2 hard sync enabled.
    /// </summary>
    [ObservableProperty]
    private bool _osc2Sync;

    /// <summary>
    /// Oscillator 2 ring modulation enabled.
    /// </summary>
    [ObservableProperty]
    private bool _osc2RingMod;

    /// <summary>
    /// Oscillator 2 routed through filter.
    /// </summary>
    [ObservableProperty]
    private bool _osc2FilterRoute = true;

    /// <summary>
    /// Oscillator 2 attack (0-15).
    /// </summary>
    [ObservableProperty]
    private int _osc2Attack = 2;

    /// <summary>
    /// Oscillator 2 decay (0-15).
    /// </summary>
    [ObservableProperty]
    private int _osc2Decay = 4;

    /// <summary>
    /// Oscillator 2 sustain (0-15).
    /// </summary>
    [ObservableProperty]
    private int _osc2Sustain = 8;

    /// <summary>
    /// Oscillator 2 release (0-15).
    /// </summary>
    [ObservableProperty]
    private int _osc2Release = 4;

    #endregion

    #region Oscillator 3 Properties

    /// <summary>
    /// Oscillator 3 waveform index.
    /// </summary>
    [ObservableProperty]
    private int _osc3WaveformIndex = 2;

    /// <summary>
    /// Oscillator 3 pulse width (0-4095).
    /// </summary>
    [ObservableProperty]
    private int _osc3PulseWidth = 2048;

    /// <summary>
    /// Oscillator 3 hard sync enabled.
    /// </summary>
    [ObservableProperty]
    private bool _osc3Sync;

    /// <summary>
    /// Oscillator 3 ring modulation enabled.
    /// </summary>
    [ObservableProperty]
    private bool _osc3RingMod;

    /// <summary>
    /// Oscillator 3 routed through filter.
    /// </summary>
    [ObservableProperty]
    private bool _osc3FilterRoute = true;

    /// <summary>
    /// Oscillator 3 attack (0-15).
    /// </summary>
    [ObservableProperty]
    private int _osc3Attack = 2;

    /// <summary>
    /// Oscillator 3 decay (0-15).
    /// </summary>
    [ObservableProperty]
    private int _osc3Decay = 4;

    /// <summary>
    /// Oscillator 3 sustain (0-15).
    /// </summary>
    [ObservableProperty]
    private int _osc3Sustain = 8;

    /// <summary>
    /// Oscillator 3 release (0-15).
    /// </summary>
    [ObservableProperty]
    private int _osc3Release = 4;

    #endregion

    #region Filter Properties

    /// <summary>
    /// Filter type index (0=LP, 1=BP, 2=HP, 3=Off).
    /// </summary>
    [ObservableProperty]
    private int _filterTypeIndex;

    /// <summary>
    /// Filter cutoff frequency (0-2047).
    /// </summary>
    [ObservableProperty]
    private int _filterCutoff = 1024;

    /// <summary>
    /// Filter resonance (0-15).
    /// </summary>
    [ObservableProperty]
    private int _filterResonance = 8;

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new SIDSynthViewModel.
    /// </summary>
    public SIDSynthViewModel()
    {
        Initialize();
    }

    #endregion

    #region Initialization

    private void Initialize()
    {
        if (_isInitialized) return;

        lock (_lock)
        {
            if (_isInitialized) return;

            try
            {
                // Create the SID synth
                _sidSynth = new SIDSynth();
                _sidSynth.Volume = Volume;

                // Create audio output
                _waveOut = new WaveOutEvent
                {
                    DesiredLatency = 50
                };
                _waveOut.Init(_sidSynth);
                _waveOut.Play();

                _isInitialized = true;
                ApplyAllParameters();
                StatusMessage = "SID Synth initialized";

                System.Diagnostics.Debug.WriteLine("[SIDSynthViewModel] Initialized successfully");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SIDSynthViewModel] Initialization failed: {ex.Message}");
                StatusMessage = $"Initialization failed: {ex.Message}";
                Cleanup();
            }
        }
    }

    #endregion

    #region Preset Commands

    /// <summary>
    /// Initializes the patch to default values.
    /// </summary>
    [RelayCommand]
    private void InitPatch()
    {
        // Reset to default values
        ChipModelIndex = 0; // 6581

        // Oscillator 1
        Osc1WaveformIndex = 2; // Pulse
        Osc1PulseWidth = 2048;
        Osc1Sync = false;
        Osc1RingMod = false;
        Osc1FilterRoute = true;
        Osc1Attack = 2;
        Osc1Decay = 4;
        Osc1Sustain = 8;
        Osc1Release = 4;

        // Oscillator 2
        Osc2WaveformIndex = 2;
        Osc2PulseWidth = 2048;
        Osc2Sync = false;
        Osc2RingMod = false;
        Osc2FilterRoute = true;
        Osc2Attack = 2;
        Osc2Decay = 4;
        Osc2Sustain = 8;
        Osc2Release = 4;

        // Oscillator 3
        Osc3WaveformIndex = 2;
        Osc3PulseWidth = 2048;
        Osc3Sync = false;
        Osc3RingMod = false;
        Osc3FilterRoute = true;
        Osc3Attack = 2;
        Osc3Decay = 4;
        Osc3Sustain = 8;
        Osc3Release = 4;

        // Filter
        FilterTypeIndex = 0; // Low Pass
        FilterCutoff = 1024;
        FilterResonance = 8;

        ApplyAllParameters();
        StatusMessage = "Initialized to default";
    }

    /// <summary>
    /// Randomizes the patch parameters.
    /// </summary>
    [RelayCommand]
    private void RandomizePatch()
    {
        var random = new Random();

        // Randomize chip model
        ChipModelIndex = random.Next(2);

        // Randomize oscillators
        for (int i = 0; i < 3; i++)
        {
            int waveform = random.Next(4);
            int pulseWidth = random.Next(4096);
            bool sync = random.NextDouble() > 0.7;
            bool ringMod = random.NextDouble() > 0.7;
            bool filterRoute = random.NextDouble() > 0.3;
            int attack = random.Next(16);
            int decay = random.Next(16);
            int sustain = random.Next(16);
            int release = random.Next(16);

            switch (i)
            {
                case 0:
                    Osc1WaveformIndex = waveform;
                    Osc1PulseWidth = pulseWidth;
                    Osc1Sync = sync;
                    Osc1RingMod = ringMod;
                    Osc1FilterRoute = filterRoute;
                    Osc1Attack = attack;
                    Osc1Decay = decay;
                    Osc1Sustain = sustain;
                    Osc1Release = release;
                    break;
                case 1:
                    Osc2WaveformIndex = waveform;
                    Osc2PulseWidth = pulseWidth;
                    Osc2Sync = sync;
                    Osc2RingMod = ringMod;
                    Osc2FilterRoute = filterRoute;
                    Osc2Attack = attack;
                    Osc2Decay = decay;
                    Osc2Sustain = sustain;
                    Osc2Release = release;
                    break;
                case 2:
                    Osc3WaveformIndex = waveform;
                    Osc3PulseWidth = pulseWidth;
                    Osc3Sync = sync;
                    Osc3RingMod = ringMod;
                    Osc3FilterRoute = filterRoute;
                    Osc3Attack = attack;
                    Osc3Decay = decay;
                    Osc3Sustain = sustain;
                    Osc3Release = release;
                    break;
            }
        }

        // Randomize filter
        FilterTypeIndex = random.Next(3); // Exclude Off for more interesting sounds
        FilterCutoff = random.Next(2048);
        FilterResonance = random.Next(16);

        ApplyAllParameters();
        StatusMessage = "Randomized patch";
    }

    /// <summary>
    /// Loads the C64 Bass preset.
    /// </summary>
    [RelayCommand]
    private void LoadC64Bass()
    {
        ChipModelIndex = 0; // 6581

        Osc1WaveformIndex = 2; // Pulse
        Osc1PulseWidth = 2048;
        Osc1Sync = false;
        Osc1RingMod = false;
        Osc1FilterRoute = true;
        Osc1Attack = 0;
        Osc1Decay = 6;
        Osc1Sustain = 4;
        Osc1Release = 2;

        Osc2WaveformIndex = 2;
        Osc2PulseWidth = 2048;
        Osc2Sync = false;
        Osc2RingMod = false;
        Osc2FilterRoute = false;
        Osc2Attack = 2;
        Osc2Decay = 4;
        Osc2Sustain = 8;
        Osc2Release = 4;

        Osc3WaveformIndex = 2;
        Osc3PulseWidth = 2048;
        Osc3Sync = false;
        Osc3RingMod = false;
        Osc3FilterRoute = false;
        Osc3Attack = 2;
        Osc3Decay = 4;
        Osc3Sustain = 8;
        Osc3Release = 4;

        FilterTypeIndex = 0; // Low Pass
        FilterCutoff = 512;
        FilterResonance = 10;

        ApplyAllParameters();
        StatusMessage = "Loaded C64 Bass preset";
    }

    /// <summary>
    /// Loads the Arp Lead preset.
    /// </summary>
    [RelayCommand]
    private void LoadArpLead()
    {
        ChipModelIndex = 0; // 6581

        Osc1WaveformIndex = 1; // Sawtooth
        Osc1PulseWidth = 2048;
        Osc1Sync = false;
        Osc1RingMod = false;
        Osc1FilterRoute = true;
        Osc1Attack = 0;
        Osc1Decay = 4;
        Osc1Sustain = 8;
        Osc1Release = 3;

        Osc2WaveformIndex = 1;
        Osc2PulseWidth = 2048;
        Osc2Sync = false;
        Osc2RingMod = false;
        Osc2FilterRoute = true;
        Osc2Attack = 0;
        Osc2Decay = 4;
        Osc2Sustain = 8;
        Osc2Release = 3;

        Osc3WaveformIndex = 1;
        Osc3PulseWidth = 2048;
        Osc3Sync = false;
        Osc3RingMod = false;
        Osc3FilterRoute = true;
        Osc3Attack = 0;
        Osc3Decay = 4;
        Osc3Sustain = 8;
        Osc3Release = 3;

        FilterTypeIndex = 0; // Low Pass
        FilterCutoff = 1200;
        FilterResonance = 8;

        ApplyAllParameters();
        StatusMessage = "Loaded Arp Lead preset";
    }

    /// <summary>
    /// Loads the Ring Bell preset.
    /// </summary>
    [RelayCommand]
    private void LoadRingBell()
    {
        ChipModelIndex = 1; // 8580

        Osc1WaveformIndex = 0; // Triangle
        Osc1PulseWidth = 2048;
        Osc1Sync = false;
        Osc1RingMod = true;
        Osc1FilterRoute = false;
        Osc1Attack = 0;
        Osc1Decay = 8;
        Osc1Sustain = 0;
        Osc1Release = 6;

        Osc2WaveformIndex = 0; // Triangle
        Osc2PulseWidth = 2048;
        Osc2Sync = false;
        Osc2RingMod = false;
        Osc2FilterRoute = false;
        Osc2Attack = 0;
        Osc2Decay = 6;
        Osc2Sustain = 0;
        Osc2Release = 4;

        Osc3WaveformIndex = 0;
        Osc3PulseWidth = 2048;
        Osc3Sync = false;
        Osc3RingMod = false;
        Osc3FilterRoute = false;
        Osc3Attack = 2;
        Osc3Decay = 4;
        Osc3Sustain = 8;
        Osc3Release = 4;

        FilterTypeIndex = 3; // Off
        FilterCutoff = 1024;
        FilterResonance = 8;

        ApplyAllParameters();
        StatusMessage = "Loaded Ring Bell preset";
    }

    /// <summary>
    /// Loads the Sync Lead preset.
    /// </summary>
    [RelayCommand]
    private void LoadSyncLead()
    {
        ChipModelIndex = 0; // 6581

        Osc1WaveformIndex = 1; // Sawtooth
        Osc1PulseWidth = 2048;
        Osc1Sync = true;
        Osc1RingMod = false;
        Osc1FilterRoute = true;
        Osc1Attack = 2;
        Osc1Decay = 6;
        Osc1Sustain = 6;
        Osc1Release = 4;

        Osc2WaveformIndex = 1; // Sawtooth
        Osc2PulseWidth = 2048;
        Osc2Sync = false;
        Osc2RingMod = false;
        Osc2FilterRoute = false;
        Osc2Attack = 2;
        Osc2Decay = 6;
        Osc2Sustain = 6;
        Osc2Release = 4;

        Osc3WaveformIndex = 1;
        Osc3PulseWidth = 2048;
        Osc3Sync = false;
        Osc3RingMod = false;
        Osc3FilterRoute = false;
        Osc3Attack = 2;
        Osc3Decay = 6;
        Osc3Sustain = 6;
        Osc3Release = 4;

        FilterTypeIndex = 0; // Low Pass
        FilterCutoff = 1500;
        FilterResonance = 6;

        ApplyAllParameters();
        StatusMessage = "Loaded Sync Lead preset";
    }

    /// <summary>
    /// Loads the Noise Hit preset.
    /// </summary>
    [RelayCommand]
    private void LoadNoiseHit()
    {
        ChipModelIndex = 0; // 6581

        Osc1WaveformIndex = 3; // Noise
        Osc1PulseWidth = 2048;
        Osc1Sync = false;
        Osc1RingMod = false;
        Osc1FilterRoute = true;
        Osc1Attack = 0;
        Osc1Decay = 3;
        Osc1Sustain = 0;
        Osc1Release = 2;

        Osc2WaveformIndex = 3;
        Osc2PulseWidth = 2048;
        Osc2Sync = false;
        Osc2RingMod = false;
        Osc2FilterRoute = false;
        Osc2Attack = 2;
        Osc2Decay = 4;
        Osc2Sustain = 8;
        Osc2Release = 4;

        Osc3WaveformIndex = 3;
        Osc3PulseWidth = 2048;
        Osc3Sync = false;
        Osc3RingMod = false;
        Osc3FilterRoute = false;
        Osc3Attack = 2;
        Osc3Decay = 4;
        Osc3Sustain = 8;
        Osc3Release = 4;

        FilterTypeIndex = 1; // Band Pass
        FilterCutoff = 800;
        FilterResonance = 12;

        ApplyAllParameters();
        StatusMessage = "Loaded Noise Hit preset";
    }

    #endregion

    #region Parameter Application

    private void ApplyAllParameters()
    {
        if (_sidSynth == null) return;

        // Apply chip revision
        _sidSynth.Revision = ChipModelIndex == 0 ? SIDRevision.MOS6581 : SIDRevision.MOS8580;

        // Apply volume
        _sidSynth.Volume = Volume;

        // Apply oscillator 1 parameters
        ApplyOscillatorParameters(0);

        // Apply oscillator 2 parameters
        ApplyOscillatorParameters(1);

        // Apply oscillator 3 parameters
        ApplyOscillatorParameters(2);

        // Apply filter parameters
        ApplyFilterParameters();
    }

    private void ApplyOscillatorParameters(int oscIndex)
    {
        if (_sidSynth == null || oscIndex < 0 || oscIndex >= 3) return;

        var osc = _sidSynth.Oscillators[oscIndex];

        switch (oscIndex)
        {
            case 0:
                osc.Waveform = IndexToWaveform(Osc1WaveformIndex);
                osc.PulseWidth = Osc1PulseWidth;
                osc.HardSync = Osc1Sync;
                osc.RingMod = Osc1RingMod;
                osc.FilterEnable = Osc1FilterRoute;
                osc.Attack = Osc1Attack;
                osc.Decay = Osc1Decay;
                osc.Sustain = Osc1Sustain;
                osc.Release = Osc1Release;
                break;
            case 1:
                osc.Waveform = IndexToWaveform(Osc2WaveformIndex);
                osc.PulseWidth = Osc2PulseWidth;
                osc.HardSync = Osc2Sync;
                osc.RingMod = Osc2RingMod;
                osc.FilterEnable = Osc2FilterRoute;
                osc.Attack = Osc2Attack;
                osc.Decay = Osc2Decay;
                osc.Sustain = Osc2Sustain;
                osc.Release = Osc2Release;
                break;
            case 2:
                osc.Waveform = IndexToWaveform(Osc3WaveformIndex);
                osc.PulseWidth = Osc3PulseWidth;
                osc.HardSync = Osc3Sync;
                osc.RingMod = Osc3RingMod;
                osc.FilterEnable = Osc3FilterRoute;
                osc.Attack = Osc3Attack;
                osc.Decay = Osc3Decay;
                osc.Sustain = Osc3Sustain;
                osc.Release = Osc3Release;
                break;
        }
    }

    private void ApplyFilterParameters()
    {
        if (_sidSynth == null) return;

        _sidSynth.FilterMode = IndexToFilterMode(FilterTypeIndex);
        _sidSynth.FilterCutoff = FilterCutoff;
        _sidSynth.FilterResonance = FilterResonance;
    }

    private static SIDWaveform IndexToWaveform(int index)
    {
        return index switch
        {
            0 => SIDWaveform.Triangle,
            1 => SIDWaveform.Sawtooth,
            2 => SIDWaveform.Pulse,
            3 => SIDWaveform.Noise,
            _ => SIDWaveform.Pulse
        };
    }

    private static SIDFilterMode IndexToFilterMode(int index)
    {
        return index switch
        {
            0 => SIDFilterMode.LowPass,
            1 => SIDFilterMode.BandPass,
            2 => SIDFilterMode.HighPass,
            3 => SIDFilterMode.Off,
            _ => SIDFilterMode.LowPass
        };
    }

    #endregion

    #region Property Change Handlers

    partial void OnVolumeChanged(float value)
    {
        if (_sidSynth != null)
        {
            _sidSynth.Volume = value;
        }
    }

    partial void OnChipModelIndexChanged(int value)
    {
        if (_sidSynth != null)
        {
            _sidSynth.Revision = value == 0 ? SIDRevision.MOS6581 : SIDRevision.MOS8580;
        }
        OnPropertyChanged(nameof(ChipModelName));
    }

    // Oscillator 1 handlers
    partial void OnOsc1WaveformIndexChanged(int value) => ApplyOscillatorParameters(0);
    partial void OnOsc1PulseWidthChanged(int value) => ApplyOscillatorParameters(0);
    partial void OnOsc1SyncChanged(bool value) => ApplyOscillatorParameters(0);
    partial void OnOsc1RingModChanged(bool value) => ApplyOscillatorParameters(0);
    partial void OnOsc1FilterRouteChanged(bool value) => ApplyOscillatorParameters(0);
    partial void OnOsc1AttackChanged(int value) => ApplyOscillatorParameters(0);
    partial void OnOsc1DecayChanged(int value) => ApplyOscillatorParameters(0);
    partial void OnOsc1SustainChanged(int value) => ApplyOscillatorParameters(0);
    partial void OnOsc1ReleaseChanged(int value) => ApplyOscillatorParameters(0);

    // Oscillator 2 handlers
    partial void OnOsc2WaveformIndexChanged(int value) => ApplyOscillatorParameters(1);
    partial void OnOsc2PulseWidthChanged(int value) => ApplyOscillatorParameters(1);
    partial void OnOsc2SyncChanged(bool value) => ApplyOscillatorParameters(1);
    partial void OnOsc2RingModChanged(bool value) => ApplyOscillatorParameters(1);
    partial void OnOsc2FilterRouteChanged(bool value) => ApplyOscillatorParameters(1);
    partial void OnOsc2AttackChanged(int value) => ApplyOscillatorParameters(1);
    partial void OnOsc2DecayChanged(int value) => ApplyOscillatorParameters(1);
    partial void OnOsc2SustainChanged(int value) => ApplyOscillatorParameters(1);
    partial void OnOsc2ReleaseChanged(int value) => ApplyOscillatorParameters(1);

    // Oscillator 3 handlers
    partial void OnOsc3WaveformIndexChanged(int value) => ApplyOscillatorParameters(2);
    partial void OnOsc3PulseWidthChanged(int value) => ApplyOscillatorParameters(2);
    partial void OnOsc3SyncChanged(bool value) => ApplyOscillatorParameters(2);
    partial void OnOsc3RingModChanged(bool value) => ApplyOscillatorParameters(2);
    partial void OnOsc3FilterRouteChanged(bool value) => ApplyOscillatorParameters(2);
    partial void OnOsc3AttackChanged(int value) => ApplyOscillatorParameters(2);
    partial void OnOsc3DecayChanged(int value) => ApplyOscillatorParameters(2);
    partial void OnOsc3SustainChanged(int value) => ApplyOscillatorParameters(2);
    partial void OnOsc3ReleaseChanged(int value) => ApplyOscillatorParameters(2);

    // Filter handlers
    partial void OnFilterTypeIndexChanged(int value) => ApplyFilterParameters();
    partial void OnFilterCutoffChanged(int value) => ApplyFilterParameters();
    partial void OnFilterResonanceChanged(int value) => ApplyFilterParameters();

    #endregion

    #region Cleanup

    private void Cleanup()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;

        _sidSynth = null;
        _isInitialized = false;
    }

    /// <summary>
    /// Disposes resources.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;

        lock (_lock)
        {
            if (_disposed) return;
            _disposed = true;

            Cleanup();
        }

        GC.SuppressFinalize(this);
    }

    #endregion
}

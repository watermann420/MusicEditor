// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Code-behind for the ChipTune Synthesizer Editor control.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using MusicEngine.Core.Synthesizers;

namespace MusicEngineEditor.Controls.Synths;

/// <summary>
/// Interaction logic for ChipTuneSynthControl.xaml.
/// A retro 8-bit chiptune synthesizer control emulating classic sound chips (NES 2A03, GameBoy DMG, C64 SID).
/// Features pulse/square waves with variable duty cycle, triangle waves, multiple noise types,
/// arpeggio, vibrato, and classic envelope shapes.
/// </summary>
public partial class ChipTuneSynthControl : UserControl, INotifyPropertyChanged
{
    private ChipTuneSynth? _synth;
    private bool _isInitializing = true;

    #region Bindable Properties

    private float _volume = 0.7f;
    /// <summary>Gets or sets the master volume (0-1).</summary>
    public float Volume
    {
        get => _volume;
        set { _volume = value; OnPropertyChanged(); UpdateSynth(); }
    }

    private double _pulseWidth = 0.5;
    /// <summary>Gets or sets the pulse width for variable pulse wave (0-1).</summary>
    public double PulseWidth
    {
        get => _pulseWidth;
        set { _pulseWidth = value; OnPropertyChanged(); UpdateSynth(); }
    }

    private double _arpeggioSpeed;
    /// <summary>Gets or sets the arpeggio speed in Hz.</summary>
    public double ArpeggioSpeed
    {
        get => _arpeggioSpeed;
        set { _arpeggioSpeed = value; OnPropertyChanged(); UpdateSynth(); }
    }

    private int _arpeggioSemitones;
    /// <summary>Gets or sets the arpeggio interval in semitones.</summary>
    public int ArpeggioSemitones
    {
        get => _arpeggioSemitones;
        set { _arpeggioSemitones = value; OnPropertyChanged(); UpdateSynth(); }
    }

    private double _vibratoDepth;
    /// <summary>Gets or sets the vibrato depth in semitones.</summary>
    public double VibratoDepth
    {
        get => _vibratoDepth;
        set { _vibratoDepth = value; OnPropertyChanged(); UpdateSynth(); }
    }

    private double _vibratoSpeed = 5;
    /// <summary>Gets or sets the vibrato speed in Hz.</summary>
    public double VibratoSpeed
    {
        get => _vibratoSpeed;
        set { _vibratoSpeed = value; OnPropertyChanged(); UpdateSynth(); }
    }

    private int _bitDepth = 8;
    /// <summary>Gets or sets the bit depth for bit-crushing (4-16).</summary>
    public int BitDepth
    {
        get => _bitDepth;
        set { _bitDepth = value; OnPropertyChanged(); UpdateSynth(); }
    }

    private double _attack = 0.01;
    /// <summary>Gets or sets the attack time in seconds.</summary>
    public double Attack
    {
        get => _attack;
        set { _attack = value; OnPropertyChanged(); UpdateSynth(); }
    }

    private double _decay = 0.1;
    /// <summary>Gets or sets the decay time in seconds.</summary>
    public double Decay
    {
        get => _decay;
        set { _decay = value; OnPropertyChanged(); UpdateSynth(); }
    }

    private double _sustain = 0.7;
    /// <summary>Gets or sets the sustain level (0-1).</summary>
    public double Sustain
    {
        get => _sustain;
        set { _sustain = value; OnPropertyChanged(); UpdateSynth(); }
    }

    private double _release = 0.1;
    /// <summary>Gets or sets the release time in seconds.</summary>
    public double Release
    {
        get => _release;
        set { _release = value; OnPropertyChanged(); UpdateSynth(); }
    }

    // Channel mixer levels
    private double _pulse1Level = 1.0;
    /// <summary>Gets or sets the Pulse 1 channel level.</summary>
    public double Pulse1Level
    {
        get => _pulse1Level;
        set { _pulse1Level = value; OnPropertyChanged(); }
    }

    private double _pulse2Level = 1.0;
    /// <summary>Gets or sets the Pulse 2 channel level.</summary>
    public double Pulse2Level
    {
        get => _pulse2Level;
        set { _pulse2Level = value; OnPropertyChanged(); }
    }

    private double _triangleLevel = 1.0;
    /// <summary>Gets or sets the Triangle/Wave channel level.</summary>
    public double TriangleLevel
    {
        get => _triangleLevel;
        set { _triangleLevel = value; OnPropertyChanged(); }
    }

    private double _noiseLevel = 1.0;
    /// <summary>Gets or sets the Noise channel level.</summary>
    public double NoiseLevel
    {
        get => _noiseLevel;
        set { _noiseLevel = value; OnPropertyChanged(); }
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Creates a new ChipTuneSynthControl.
    /// </summary>
    public ChipTuneSynthControl()
    {
        InitializeComponent();
        DataContext = this;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    #endregion

    #region Lifecycle

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = false;
        UpdateChipNameDisplay();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        // Cleanup if needed
    }

    #endregion

    #region Synth Binding

    /// <summary>
    /// Gets or sets the ChipTuneSynth instance to control.
    /// </summary>
    public ChipTuneSynth? Synth
    {
        get => _synth;
        set
        {
            _synth = value;
            if (_synth != null)
            {
                LoadFromSynth();
            }
        }
    }

    /// <summary>
    /// Loads current parameter values from the synth.
    /// </summary>
    private void LoadFromSynth()
    {
        if (_synth == null) return;

        _isInitializing = true;

        Volume = _synth.Volume;
        PulseWidth = _synth.PulseWidth;
        ArpeggioSpeed = _synth.ArpeggioSpeed;
        ArpeggioSemitones = _synth.ArpeggioSemitones;
        VibratoDepth = _synth.VibratoDepth;
        VibratoSpeed = _synth.VibratoSpeed;
        BitDepth = _synth.BitDepth;
        Attack = _synth.Attack;
        Decay = _synth.Decay;
        Sustain = _synth.Sustain;
        Release = _synth.Release;

        // Set chip type radio button
        switch (_synth.EmulationMode)
        {
            case ChipEmulationMode.NES:
                NESRadio.IsChecked = true;
                break;
            case ChipEmulationMode.GameBoy:
                GameBoyRadio.IsChecked = true;
                break;
            case ChipEmulationMode.C64:
                C64Radio.IsChecked = true;
                break;
            default:
                NESRadio.IsChecked = true;
                break;
        }

        // Set duty cycle radio button
        switch (_synth.Waveform)
        {
            case ChipWaveform.Pulse12:
                Duty12Radio.IsChecked = true;
                break;
            case ChipWaveform.Pulse25:
                Duty25Radio.IsChecked = true;
                break;
            case ChipWaveform.Pulse50:
            default:
                Duty50Radio.IsChecked = true;
                break;
        }

        // Set noise type
        switch (_synth.NoiseType)
        {
            case ChipNoiseType.Periodic:
                PeriodicNoiseToggle.IsChecked = true;
                WhiteNoiseToggle.IsChecked = false;
                break;
            case ChipNoiseType.White:
            default:
                WhiteNoiseToggle.IsChecked = true;
                PeriodicNoiseToggle.IsChecked = false;
                break;
        }

        _isInitializing = false;
    }

    /// <summary>
    /// Updates the synth with current parameter values.
    /// </summary>
    private void UpdateSynth()
    {
        if (_synth == null || _isInitializing) return;

        _synth.Volume = Volume;
        _synth.PulseWidth = PulseWidth;
        _synth.ArpeggioSpeed = ArpeggioSpeed;
        _synth.ArpeggioSemitones = ArpeggioSemitones;
        _synth.VibratoDepth = VibratoDepth;
        _synth.VibratoSpeed = VibratoSpeed;
        _synth.BitDepth = BitDepth;
        _synth.Attack = Attack;
        _synth.Decay = Decay;
        _synth.Sustain = Sustain;
        _synth.Release = Release;
    }

    #endregion

    #region Event Handlers

    private void OnChipTypeChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _synth == null) return;

        if (NESRadio.IsChecked == true)
        {
            _synth.EmulationMode = ChipEmulationMode.NES;
        }
        else if (GameBoyRadio.IsChecked == true)
        {
            _synth.EmulationMode = ChipEmulationMode.GameBoy;
        }
        else if (C64Radio.IsChecked == true)
        {
            _synth.EmulationMode = ChipEmulationMode.C64;
        }

        UpdateChipNameDisplay();
    }

    private void UpdateChipNameDisplay()
    {
        if (ChipNameDisplay == null) return;

        if (NESRadio?.IsChecked == true)
        {
            ChipNameDisplay.Text = "NES (2A03)";
            if (TriangleLabel != null) TriangleLabel.Text = "TRI";
        }
        else if (GameBoyRadio?.IsChecked == true)
        {
            ChipNameDisplay.Text = "GameBoy (DMG)";
            if (TriangleLabel != null) TriangleLabel.Text = "WAV";
        }
        else if (C64Radio?.IsChecked == true)
        {
            ChipNameDisplay.Text = "C64 (SID)";
            if (TriangleLabel != null) TriangleLabel.Text = "TRI";
        }
    }

    private void OnDutyCycleChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _synth == null) return;

        if (Duty12Radio.IsChecked == true)
        {
            _synth.Waveform = ChipWaveform.Pulse12;
            PulseWidth = 0.125;
        }
        else if (Duty25Radio.IsChecked == true)
        {
            _synth.Waveform = ChipWaveform.Pulse25;
            PulseWidth = 0.25;
        }
        else if (Duty50Radio.IsChecked == true)
        {
            _synth.Waveform = ChipWaveform.Pulse50;
            PulseWidth = 0.5;
        }
    }

    private void OnNoiseTypeChanged(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _synth == null) return;

        // Handle mutual exclusivity for toggle buttons
        if (sender == PeriodicNoiseToggle && PeriodicNoiseToggle.IsChecked == true)
        {
            WhiteNoiseToggle.IsChecked = false;
            _synth.NoiseType = ChipNoiseType.Periodic;
        }
        else if (sender == WhiteNoiseToggle && WhiteNoiseToggle.IsChecked == true)
        {
            PeriodicNoiseToggle.IsChecked = false;
            _synth.NoiseType = ChipNoiseType.White;
        }
        else if (sender == PeriodicNoiseToggle && PeriodicNoiseToggle.IsChecked == false)
        {
            // If periodic was unchecked, ensure white is checked
            if (WhiteNoiseToggle.IsChecked == false)
            {
                WhiteNoiseToggle.IsChecked = true;
                _synth.NoiseType = ChipNoiseType.White;
            }
        }
        else if (sender == WhiteNoiseToggle && WhiteNoiseToggle.IsChecked == false)
        {
            // If white was unchecked, ensure periodic is checked
            if (PeriodicNoiseToggle.IsChecked == false)
            {
                PeriodicNoiseToggle.IsChecked = true;
                _synth.NoiseType = ChipNoiseType.Periodic;
            }
        }
    }

    private void OnParameterChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_isInitializing) return;
        UpdateSynth();
    }

    private void OnMixerChanged(object sender, RoutedEventArgs e)
    {
        // Mixer changes are handled through bindings
        // This event is for potential additional logic like muting
        if (_isInitializing) return;

#if DEBUG
        // Log mixer state if needed for debugging
        System.Diagnostics.Debug.WriteLine($"[ChipTuneSynthControl] Mixer changed: P1={Pulse1Level:F2}, P2={Pulse2Level:F2}, TRI={TriangleLevel:F2}, NSE={NoiseLevel:F2}");
#endif
    }

    private void OnArpeggioPresetClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string pattern)
        {
            // Parse the pattern (e.g., "4,7" for major, "3,7" for minor, "12" for octave)
            var parts = pattern.Split(',');
            if (parts.Length > 0 && int.TryParse(parts[0], out int semitones))
            {
                ArpeggioSemitones = semitones;

                // Set a default arpeggio speed if not already set
                if (ArpeggioSpeed < 1)
                {
                    ArpeggioSpeed = 15; // 15 Hz is a good default for classic chiptune arpeggios
                }
            }
        }
    }

    #endregion

    #region INotifyPropertyChanged

    /// <summary>
    /// Occurs when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// Raises the PropertyChanged event.
    /// </summary>
    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    #endregion
}

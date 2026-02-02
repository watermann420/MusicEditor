// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Organ Synth Editor control.

using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Synthesizers;

namespace MusicEngineEditor.ViewModels.Synths;

/// <summary>
/// Represents a Hammond-style drawbar in the organ synth editor.
/// </summary>
public partial class OrganDrawbarViewModel : ObservableObject
{
    private readonly OrganSynthViewModel _parent;
    private readonly int _index;

    [ObservableProperty]
    private int _value;

    [ObservableProperty]
    private string _footage = "";

    [ObservableProperty]
    private SolidColorBrush _colorBrush = Brushes.White;

    /// <summary>
    /// Gets the drawbar index (0-8).
    /// </summary>
    public int Index => _index;

    public OrganDrawbarViewModel(int index, string footage, SolidColorBrush colorBrush, OrganSynthViewModel parent)
    {
        _index = index;
        _footage = footage;
        _colorBrush = colorBrush;
        _parent = parent;
        _value = 0;
    }

    partial void OnValueChanged(int value)
    {
        _parent.UpdateDrawbar(_index, value);
    }
}

/// <summary>
/// Vibrato/Chorus setting enumeration.
/// </summary>
public enum VibratoChorusSetting
{
    V1, V2, V3, C1, C2, C3
}

/// <summary>
/// ViewModel for the Organ Synth Editor control.
/// Provides UI bindings for tonewheel organ synthesis with drawbars,
/// percussion, vibrato/chorus, and Leslie rotary speaker.
/// </summary>
public partial class OrganSynthViewModel : ViewModelBase, IDisposable
{
    private OrganSynth? _synth;
    private bool _disposed;
    private bool _isUpdating;

    #region Observable Properties - General

    [ObservableProperty]
    private string _presetName = "Custom";

    [ObservableProperty]
    private float _volume = 0.5f;

    #endregion

    #region Observable Properties - Percussion

    [ObservableProperty]
    private bool _percussionEnabled = true;

    [ObservableProperty]
    private float _percussionLevel = 0.5f;

    [ObservableProperty]
    private bool _isPercussionSlow = true;

    [ObservableProperty]
    private bool _isPercussionFast;

    [ObservableProperty]
    private bool _isPercussionSecond = true;

    [ObservableProperty]
    private bool _isPercussionThird;

    #endregion

    #region Observable Properties - Vibrato/Chorus

    [ObservableProperty]
    private bool _isVibratoV1;

    [ObservableProperty]
    private bool _isVibratoV2;

    [ObservableProperty]
    private bool _isVibratoV3;

    [ObservableProperty]
    private bool _isChorusC1 = true;

    [ObservableProperty]
    private bool _isChorusC2;

    [ObservableProperty]
    private bool _isChorusC3;

    [ObservableProperty]
    private float _keyClickLevel = 0.3f;

    [ObservableProperty]
    private float _tonewheelLeakage = 0.2f;

    #endregion

    #region Observable Properties - Rotary Speaker

    [ObservableProperty]
    private bool _rotaryEnabled = true;

    [ObservableProperty]
    private bool _isRotaryStop;

    [ObservableProperty]
    private bool _isRotarySlow = true;

    [ObservableProperty]
    private bool _isRotaryFast;

    [ObservableProperty]
    private float _rotaryMix = 0.8f;

    [ObservableProperty]
    private float _hornLevel = 0.7f;

    [ObservableProperty]
    private float _drumLevel = 0.5f;

    [ObservableProperty]
    private float _overdrive;

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets the current drawbar settings as a string (e.g., "888000000").
    /// </summary>
    public string DrawbarString => string.Concat(Drawbars.Select(d => d.Value.ToString()));

    /// <summary>
    /// Gets the percussion status text.
    /// </summary>
    public string PercussionStatus
    {
        get
        {
            if (!PercussionEnabled) return "Off";
            string harmonic = IsPercussionSecond ? "2nd" : "3rd";
            string decay = IsPercussionFast ? "Fast" : "Slow";
            return $"{harmonic}, {decay}";
        }
    }

    /// <summary>
    /// Gets the vibrato/chorus status text.
    /// </summary>
    public string VibratoChorusStatus
    {
        get
        {
            if (IsVibratoV1) return "V1";
            if (IsVibratoV2) return "V2";
            if (IsVibratoV3) return "V3";
            if (IsChorusC1) return "C1";
            if (IsChorusC2) return "C2";
            if (IsChorusC3) return "C3";
            return "Off";
        }
    }

    /// <summary>
    /// Gets the rotary speaker status text.
    /// </summary>
    public string RotaryStatus
    {
        get
        {
            if (!RotaryEnabled) return "Off";
            if (IsRotaryStop) return "Stop";
            if (IsRotarySlow) return "Slow";
            if (IsRotaryFast) return "Fast";
            return "Off";
        }
    }

    /// <summary>
    /// Gets the rotary speed text for the status bar.
    /// </summary>
    public string RotarySpeedText
    {
        get
        {
            if (!RotaryEnabled) return "OFF";
            if (IsRotaryStop) return "STOP";
            if (IsRotarySlow) return "SLOW";
            if (IsRotaryFast) return "FAST";
            return "OFF";
        }
    }

    #endregion

    #region Collections

    /// <summary>
    /// Gets the collection of Hammond-style drawbars (9 drawbars).
    /// </summary>
    public ObservableCollection<OrganDrawbarViewModel> Drawbars { get; } = new();

    #endregion

    #region Events

    /// <summary>
    /// Raised when a note should be previewed.
    /// </summary>
    public event EventHandler<int>? PreviewNote;

    #endregion

    #region Constructor and Initialization

    public OrganSynthViewModel()
    {
        InitializeDrawbars();
    }

    public OrganSynthViewModel(OrganSynth synth) : this()
    {
        SetSynth(synth);
    }

    /// <summary>
    /// Sets or replaces the underlying OrganSynth instance.
    /// </summary>
    public void SetSynth(OrganSynth synth)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        LoadFromSynth();
    }

    /// <summary>
    /// Creates a new OrganSynth instance and initializes the editor.
    /// </summary>
    public void Initialize(int? sampleRate = null)
    {
        _synth = new OrganSynth(sampleRate);
        LoadFromSynth();
    }

    private void LoadFromSynth()
    {
        if (_synth == null) return;

        _isUpdating = true;

        try
        {
            // Load master settings
            Volume = _synth.Volume;
            PresetName = _synth.Name;

            // Load drawbars
            for (int i = 0; i < 9 && i < Drawbars.Count; i++)
            {
                Drawbars[i].Value = _synth.Drawbars[i];
            }

            // Load percussion settings
            PercussionEnabled = _synth.PercussionEnabled;
            PercussionLevel = _synth.PercussionLevel;
            IsPercussionFast = _synth.PercussionFast;
            IsPercussionSlow = !_synth.PercussionFast;
            IsPercussionSecond = _synth.PercussionHarmonic == PercussionHarmonic.Second;
            IsPercussionThird = _synth.PercussionHarmonic == PercussionHarmonic.Third;

            // Load key click and tonewheel settings
            KeyClickLevel = _synth.KeyClickLevel;
            TonewheelLeakage = _synth.TonewheelLeakage;

            // Load rotary speaker settings
            RotaryEnabled = _synth.RotaryEnabled;
            IsRotaryStop = _synth.RotarySpeedSetting == RotarySpeed.Stop;
            IsRotarySlow = _synth.RotarySpeedSetting == RotarySpeed.Slow;
            IsRotaryFast = _synth.RotarySpeedSetting == RotarySpeed.Fast;
            RotaryMix = _synth.RotaryMix;
            HornLevel = _synth.HornLevel;
            DrumLevel = _synth.DrumLevel;

            OnPropertyChanged(nameof(DrawbarString));
            OnPropertyChanged(nameof(PercussionStatus));
            OnPropertyChanged(nameof(VibratoChorusStatus));
            OnPropertyChanged(nameof(RotaryStatus));
            OnPropertyChanged(nameof(RotarySpeedText));
        }
        finally
        {
            _isUpdating = false;
        }
    }

    private void InitializeDrawbars()
    {
        // Hammond drawbar configuration
        // Index, Footage, Color (traditional Hammond colors)
        var brownBrush = new SolidColorBrush(Color.FromRgb(0x8B, 0x45, 0x13));
        var whiteBrush = new SolidColorBrush(Color.FromRgb(0xF0, 0xF0, 0xF0));
        var blackBrush = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x2A));

        var drawbarConfig = new[]
        {
            (0, "16'", brownBrush),      // Sub-octave
            (1, "5-1/3'", brownBrush),   // Third harmonic of sub
            (2, "8'", whiteBrush),       // Fundamental
            (3, "4'", whiteBrush),       // 2nd harmonic
            (4, "2-2/3'", blackBrush),   // 3rd harmonic
            (5, "2'", whiteBrush),       // 4th harmonic
            (6, "1-3/5'", blackBrush),   // 5th harmonic
            (7, "1-1/3'", blackBrush),   // 6th harmonic
            (8, "1'", whiteBrush),       // 8th harmonic
        };

        Drawbars.Clear();
        foreach (var (index, footage, color) in drawbarConfig)
        {
            Drawbars.Add(new OrganDrawbarViewModel(index, footage, color, this));
        }
    }

    #endregion

    #region Internal Update Methods (called by child ViewModels)

    internal void UpdateDrawbar(int index, int value)
    {
        if (_synth == null || _isUpdating) return;

        _synth.Drawbars[index] = Math.Clamp(value, 0, 8);
        PresetName = "Custom";
        OnPropertyChanged(nameof(DrawbarString));
    }

    #endregion

    #region Property Change Handlers

    partial void OnVolumeChanged(float value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.Volume = value;
        }
    }

    partial void OnPercussionEnabledChanged(bool value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.PercussionEnabled = value;
            OnPropertyChanged(nameof(PercussionStatus));
        }
    }

    partial void OnPercussionLevelChanged(float value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.PercussionLevel = value;
        }
    }

    partial void OnIsPercussionSlowChanged(bool value)
    {
        if (_synth != null && !_isUpdating && value)
        {
            _synth.PercussionFast = false;
            OnPropertyChanged(nameof(PercussionStatus));
        }
    }

    partial void OnIsPercussionFastChanged(bool value)
    {
        if (_synth != null && !_isUpdating && value)
        {
            _synth.PercussionFast = true;
            OnPropertyChanged(nameof(PercussionStatus));
        }
    }

    partial void OnIsPercussionSecondChanged(bool value)
    {
        if (_synth != null && !_isUpdating && value)
        {
            _synth.PercussionHarmonic = PercussionHarmonic.Second;
            OnPropertyChanged(nameof(PercussionStatus));
        }
    }

    partial void OnIsPercussionThirdChanged(bool value)
    {
        if (_synth != null && !_isUpdating && value)
        {
            _synth.PercussionHarmonic = PercussionHarmonic.Third;
            OnPropertyChanged(nameof(PercussionStatus));
        }
    }

    partial void OnKeyClickLevelChanged(float value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.KeyClickLevel = value;
        }
    }

    partial void OnTonewheelLeakageChanged(float value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.TonewheelLeakage = value;
        }
    }

    partial void OnRotaryEnabledChanged(bool value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.RotaryEnabled = value;
            OnPropertyChanged(nameof(RotaryStatus));
            OnPropertyChanged(nameof(RotarySpeedText));
        }
    }

    partial void OnIsRotaryStopChanged(bool value)
    {
        if (_synth != null && !_isUpdating && value)
        {
            _synth.SetRotarySpeed(RotarySpeed.Stop);
            OnPropertyChanged(nameof(RotaryStatus));
            OnPropertyChanged(nameof(RotarySpeedText));
        }
    }

    partial void OnIsRotarySlowChanged(bool value)
    {
        if (_synth != null && !_isUpdating && value)
        {
            _synth.SetRotarySpeed(RotarySpeed.Slow);
            OnPropertyChanged(nameof(RotaryStatus));
            OnPropertyChanged(nameof(RotarySpeedText));
        }
    }

    partial void OnIsRotaryFastChanged(bool value)
    {
        if (_synth != null && !_isUpdating && value)
        {
            _synth.SetRotarySpeed(RotarySpeed.Fast);
            OnPropertyChanged(nameof(RotaryStatus));
            OnPropertyChanged(nameof(RotarySpeedText));
        }
    }

    partial void OnRotaryMixChanged(float value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.RotaryMix = value;
        }
    }

    partial void OnHornLevelChanged(float value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.HornLevel = value;
        }
    }

    partial void OnDrumLevelChanged(float value)
    {
        if (_synth != null && !_isUpdating)
        {
            _synth.DrumLevel = value;
        }
    }

    // Note: Overdrive is a UI parameter that could be applied via saturation/distortion
    // The OrganSynth already applies soft clipping via tanh, but we expose this for future expansion
    partial void OnOverdriveChanged(float value)
    {
        // Future: Could apply additional overdrive/saturation effect
        // Currently the OrganSynth applies soft clipping via MathF.Tanh
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void LoadPreset(string? presetName)
    {
        if (string.IsNullOrEmpty(presetName)) return;

        OrganSynth? newSynth = presetName switch
        {
            "HammondB3" => OrganSynth.CreateHammondB3(),
            "FullOrgan" => OrganSynth.CreateFullOrgan(),
            "Gospel" => OrganSynth.CreateGospelOrgan(),
            "Rock" => OrganSynth.CreateRockOrgan(),
            "Ballad" => OrganSynth.CreateBalladOrgan(),
            "Theatre" => OrganSynth.CreateTheatreOrgan(),
            _ => null
        };

        if (newSynth != null)
        {
            // Copy settings from the preset synth to our current synth
            if (_synth != null)
            {
                _isUpdating = true;
                try
                {
                    // Copy drawbars
                    for (int i = 0; i < 9; i++)
                    {
                        _synth.Drawbars[i] = newSynth.Drawbars[i];
                        if (i < Drawbars.Count)
                        {
                            Drawbars[i].Value = newSynth.Drawbars[i];
                        }
                    }

                    // Copy percussion settings
                    _synth.PercussionEnabled = newSynth.PercussionEnabled;
                    _synth.PercussionHarmonic = newSynth.PercussionHarmonic;
                    _synth.PercussionFast = newSynth.PercussionFast;
                    _synth.PercussionLevel = newSynth.PercussionLevel;

                    // Copy key click and tonewheel
                    _synth.KeyClickLevel = newSynth.KeyClickLevel;
                    _synth.TonewheelLeakage = newSynth.TonewheelLeakage;

                    // Copy rotary settings
                    _synth.RotaryEnabled = newSynth.RotaryEnabled;
                    _synth.SetRotarySpeed(newSynth.RotarySpeedSetting);
                    _synth.RotaryMix = newSynth.RotaryMix;
                    _synth.HornLevel = newSynth.HornLevel;
                    _synth.DrumLevel = newSynth.DrumLevel;

                    _synth.Name = newSynth.Name;

                    // Update ViewModel properties
                    LoadFromSynth();
                }
                finally
                {
                    _isUpdating = false;
                }

                StatusMessage = $"Loaded preset: {newSynth.Name}";
            }
        }
    }

    [RelayCommand]
    private void ApplyDrawbarPreset(string? presetCode)
    {
        if (_synth == null || string.IsNullOrEmpty(presetCode)) return;

        _synth.SetDrawbars(presetCode);

        _isUpdating = true;
        try
        {
            for (int i = 0; i < 9 && i < Drawbars.Count; i++)
            {
                Drawbars[i].Value = _synth.Drawbars[i];
            }
        }
        finally
        {
            _isUpdating = false;
        }

        PresetName = "Custom";
        OnPropertyChanged(nameof(DrawbarString));
        StatusMessage = $"Applied drawbar preset: {presetCode}";
    }

    [RelayCommand]
    private void ResetDrawbars()
    {
        if (_synth == null) return;

        _isUpdating = true;
        try
        {
            for (int i = 0; i < 9; i++)
            {
                _synth.Drawbars[i] = 0;
                if (i < Drawbars.Count)
                {
                    Drawbars[i].Value = 0;
                }
            }
        }
        finally
        {
            _isUpdating = false;
        }

        PresetName = "Custom";
        OnPropertyChanged(nameof(DrawbarString));
        StatusMessage = "Reset all drawbars to 0";
    }

    [RelayCommand]
    private void PlayPreviewNote(int? midiNote = null)
    {
        int note = midiNote ?? 60; // Default to middle C
        PreviewNote?.Invoke(this, note);
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Gets the underlying OrganSynth instance.
    /// </summary>
    public OrganSynth? GetSynth() => _synth;

    /// <summary>
    /// Triggers a note on the synth.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        _synth?.NoteOn(note, velocity);
    }

    /// <summary>
    /// Triggers a note off on the synth.
    /// </summary>
    public void NoteOff(int note)
    {
        _synth?.NoteOff(note);
    }

    /// <summary>
    /// Stops all notes.
    /// </summary>
    public void AllNotesOff()
    {
        _synth?.AllNotesOff();
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _synth = null;
        GC.SuppressFinalize(this);
    }

    #endregion
}

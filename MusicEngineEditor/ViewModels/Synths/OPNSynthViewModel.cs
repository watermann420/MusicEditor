// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the OPN/YM2612 Synthesizer Editor.

using System;
using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Synthesizers;

namespace MusicEngineEditor.ViewModels.Synths;

/// <summary>
/// ViewModel for a single OPN operator (YM2612 style).
/// </summary>
public partial class OPNOperatorViewModel : ObservableObject
{
    private readonly OPNSynthViewModel _parentVm;
    private readonly int _operatorIndex;

    [ObservableProperty]
    private int _operatorNumber;

    [ObservableProperty]
    private int _totalLevel = 0;

    [ObservableProperty]
    private int _multiple = 1;

    [ObservableProperty]
    private int _detune = 0;

    [ObservableProperty]
    private int _keyScale = 0;

    [ObservableProperty]
    private int _attackRate = 31;

    [ObservableProperty]
    private int _decay1Rate = 10;

    [ObservableProperty]
    private int _decay2Rate = 5;

    [ObservableProperty]
    private int _sustainLevel = 5;

    [ObservableProperty]
    private int _releaseRate = 7;

    [ObservableProperty]
    private bool _ssgEgEnabled = false;

    [ObservableProperty]
    private int _amSensitivity = 0;

    [ObservableProperty]
    private bool _isCarrier;

    [ObservableProperty]
    private Color _operatorColor;

    /// <summary>
    /// Gets the display name for the operator.
    /// </summary>
    public string DisplayName => $"OP{OperatorNumber}";

    /// <summary>
    /// Gets the display string for the multiple value.
    /// </summary>
    public string MultipleDisplay => Multiple == 0 ? "0.5x" : $"{Multiple}x";

    public OPNOperatorViewModel(OPNSynthViewModel parentVm, int operatorIndex)
    {
        _parentVm = parentVm ?? throw new ArgumentNullException(nameof(parentVm));
        _operatorIndex = operatorIndex;
        _operatorNumber = operatorIndex + 1;

        // Set operator color based on index (YM2612 style - 4 operators)
        _operatorColor = operatorIndex switch
        {
            0 => Color.FromRgb(0xFF, 0x6B, 0x6B),  // Red - Operator 1
            1 => Color.FromRgb(0xFF, 0xD9, 0x3D),  // Yellow - Operator 2
            2 => Color.FromRgb(0x6B, 0xFF, 0x6B),  // Green - Operator 3
            3 => Color.FromRgb(0x00, 0xD9, 0xFF),  // Cyan - Operator 4
            _ => Color.FromRgb(0x80, 0x80, 0x80)
        };
    }

    /// <summary>
    /// Loads parameters from the synth channel.
    /// </summary>
    public void LoadFromChannel(OPNChannel channel)
    {
        if (_operatorIndex >= channel.Operators.Length) return;

        var op = channel.Operators[_operatorIndex];
        TotalLevel = op.TotalLevel;
        Multiple = op.Multiple;
        Detune = op.Detune;
        KeyScale = op.KeyScale;
        AttackRate = op.AttackRate;
        Decay1Rate = op.Decay1Rate;
        Decay2Rate = op.Decay2Rate;
        SustainLevel = op.SustainLevel;
        ReleaseRate = op.ReleaseRate;
        SsgEgEnabled = op.SsgEg > 0;
        AmSensitivity = op.AmSensitivity;

        // Notify dependent properties changed
        OnPropertyChanged(nameof(MultipleDisplay));
        OnPropertyChanged(nameof(IsCarrier));
    }

    /// <summary>
    /// Applies current values to the synth channel.
    /// </summary>
    public void ApplyToChannel(OPNChannel channel)
    {
        if (_operatorIndex >= channel.Operators.Length) return;

        var op = channel.Operators[_operatorIndex];
        op.TotalLevel = TotalLevel;
        op.Multiple = Multiple;
        op.Detune = Detune;
        op.KeyScale = KeyScale;
        op.AttackRate = AttackRate;
        op.Decay1Rate = Decay1Rate;
        op.Decay2Rate = Decay2Rate;
        op.SustainLevel = SustainLevel;
        op.ReleaseRate = ReleaseRate;
        op.SsgEg = SsgEgEnabled ? 1 : 0;
        op.AmSensitivity = AmSensitivity;
    }

    partial void OnTotalLevelChanged(int value)
    {
        _parentVm.ApplyOperatorParameter(_operatorIndex, "tl", value);
    }

    partial void OnMultipleChanged(int value)
    {
        _parentVm.ApplyOperatorParameter(_operatorIndex, "mul", value);
        OnPropertyChanged(nameof(MultipleDisplay));
    }

    partial void OnDetuneChanged(int value)
    {
        _parentVm.ApplyOperatorParameter(_operatorIndex, "dt", value);
    }

    partial void OnKeyScaleChanged(int value)
    {
        _parentVm.ApplyOperatorParameter(_operatorIndex, "ks", value);
    }

    partial void OnAttackRateChanged(int value)
    {
        _parentVm.ApplyOperatorParameter(_operatorIndex, "ar", value);
    }

    partial void OnDecay1RateChanged(int value)
    {
        _parentVm.ApplyOperatorParameter(_operatorIndex, "d1r", value);
    }

    partial void OnDecay2RateChanged(int value)
    {
        _parentVm.ApplyOperatorParameter(_operatorIndex, "d2r", value);
    }

    partial void OnSustainLevelChanged(int value)
    {
        _parentVm.ApplyOperatorParameter(_operatorIndex, "sl", value);
    }

    partial void OnReleaseRateChanged(int value)
    {
        _parentVm.ApplyOperatorParameter(_operatorIndex, "rr", value);
    }

    partial void OnSsgEgEnabledChanged(bool value)
    {
        _parentVm.ApplyOperatorParameter(_operatorIndex, "ssg", value ? 1 : 0);
    }

    /// <summary>
    /// Resets this operator to default values.
    /// </summary>
    public void Reset()
    {
        TotalLevel = _operatorIndex == 0 ? 20 : 40;
        Multiple = 1;
        Detune = 0;
        KeyScale = 0;
        AttackRate = 31;
        Decay1Rate = 10;
        Decay2Rate = 5;
        SustainLevel = 5;
        ReleaseRate = 7;
        SsgEgEnabled = false;
    }
}

/// <summary>
/// ViewModel for the OPN/YM2612 Synthesizer Editor.
/// </summary>
public partial class OPNSynthViewModel : ViewModelBase, IDisposable
{
    private OPNSynth? _synth;
    private bool _disposed;

    [ObservableProperty]
    private int _algorithm = 4;

    [ObservableProperty]
    private int _feedback = 0;

    [ObservableProperty]
    private float _masterVolume = 0.5f;

    [ObservableProperty]
    private int _activeChannel = 0;

    [ObservableProperty]
    private bool _lfoEnabled = false;

    [ObservableProperty]
    private int _lfoFrequency = 0;

    [ObservableProperty]
    private int _pmSensitivity = 0;

    [ObservableProperty]
    private int _amSensitivity = 0;

    [ObservableProperty]
    private bool _panLeft = false;

    [ObservableProperty]
    private bool _panCenter = true;

    [ObservableProperty]
    private bool _panRight = false;

    [ObservableProperty]
    private string _presetName = "Init";

    [ObservableProperty]
    private OPNOperatorViewModel? _selectedOperator;

    // LFO frequency table (Hz) - matches YM2612
    private static readonly double[] LfoFrequencies = { 3.98, 5.56, 6.02, 6.37, 6.88, 9.63, 48.1, 72.2 };

    /// <summary>
    /// Gets the collection of operator view models.
    /// </summary>
    public ObservableCollection<OPNOperatorViewModel> Operators { get; } = new();

    /// <summary>
    /// Gets the available channel indices.
    /// </summary>
    public static string[] AvailableChannels { get; } = { "CH 1", "CH 2", "CH 3", "CH 4", "CH 5", "CH 6" };

    /// <summary>
    /// Gets the algorithm description text.
    /// </summary>
    public string AlgorithmDescription => Algorithm switch
    {
        0 => "Algorithm 0: Serial chain\n4 -> 3 -> 2 -> 1 (out)\nClassic stacked FM, very harmonic-rich",
        1 => "Algorithm 1: Split parallel\n4 -> 3 -> 2 + 1 (both output)\nTwo carriers for richer sound",
        2 => "Algorithm 2: Dual pairs\n(4 -> 3) + (2 -> 1)\nTwo independent modulator-carrier pairs",
        3 => "Algorithm 3: Fork modulation\n4 -> 3 -> (2 + 1)\nOne modulator feeds two carriers",
        4 => "Algorithm 4: Dual pairs (common)\n(4 -> 3) + (2 -> 1)\nMost common Genesis algorithm",
        5 => "Algorithm 5: One-to-three\n4 -> (3 + 2 + 1)\nOne modulator, three carriers",
        6 => "Algorithm 6: Three carriers\n(4 -> 3) + 2 + 1\nBright, organ-like tones",
        7 => "Algorithm 7: All parallel\n4 + 3 + 2 + 1 (all output)\nAdditive synthesis, organ sound",
        _ => "Unknown algorithm"
    };

    /// <summary>
    /// Gets a short description of the current algorithm.
    /// </summary>
    public string AlgorithmShortDescription => Algorithm switch
    {
        0 => "4->3->2->1",
        1 => "4->3->2 + 1",
        2 => "(4->3) + (2->1)",
        3 => "4->3->(2+1)",
        4 => "(4->3) + (2->1)",
        5 => "4->(3+2+1)",
        6 => "(4->3) + 2 + 1",
        7 => "4 + 3 + 2 + 1",
        _ => "???"
    };

    /// <summary>
    /// Gets the LFO frequency display string.
    /// </summary>
    public string LfoFrequencyDisplay => $"{LfoFrequencies[Math.Clamp(LfoFrequency, 0, 7)]:F2} Hz";

    /// <summary>
    /// Gets the name of the selected operator.
    /// </summary>
    public string SelectedOperatorName => SelectedOperator?.DisplayName ?? "None";

    /// <summary>
    /// Gets the color of the selected operator.
    /// </summary>
    public Color SelectedOperatorColor => SelectedOperator?.OperatorColor ?? Colors.Gray;

    public OPNSynthViewModel()
    {
        // Design-time constructor
    }

    public OPNSynthViewModel(OPNSynth synth)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        LoadFromSynth();
    }

    /// <summary>
    /// Initializes with a new OPNSynth instance.
    /// </summary>
    public void Initialize(int? sampleRate = null)
    {
        _synth = new OPNSynth(sampleRate);
        LoadFromSynth();
    }

    /// <summary>
    /// Loads the current state from the synth.
    /// </summary>
    private void LoadFromSynth()
    {
        if (_synth == null) return;

        Operators.Clear();
        for (int i = 0; i < 4; i++)
        {
            var opVm = new OPNOperatorViewModel(this, i);
            Operators.Add(opVm);
        }

        LoadFromChannel();
        UpdateCarrierStatus();

        MasterVolume = _synth.Volume;
        ActiveChannel = _synth.ActiveChannel;
        LfoEnabled = _synth.LfoEnabled;
        LfoFrequency = _synth.LfoFrequency;
        PresetName = _synth.Name;

        OnPropertyChanged(nameof(LfoFrequencyDisplay));
        OnPropertyChanged(nameof(AlgorithmDescription));
        OnPropertyChanged(nameof(AlgorithmShortDescription));

        if (Operators.Count > 0)
        {
            SelectedOperator = Operators[0];
        }
    }

    /// <summary>
    /// Loads parameters from the current active channel.
    /// </summary>
    private void LoadFromChannel()
    {
        if (_synth == null || ActiveChannel < 0 || ActiveChannel >= 6) return;

        var channel = _synth.Channels[ActiveChannel];
        Algorithm = (int)channel.Algorithm;
        Feedback = channel.Feedback;
        PmSensitivity = channel.FmSensitivity;
        AmSensitivity = channel.AmSensitivity;

        // Panning
        PanLeft = channel.Panning == 2;
        PanCenter = channel.Panning == 3;
        PanRight = channel.Panning == 1;

        foreach (var op in Operators)
        {
            op.LoadFromChannel(channel);
        }
    }

    /// <summary>
    /// Updates the carrier status for each operator based on the current algorithm.
    /// </summary>
    private void UpdateCarrierStatus()
    {
        // YM2612 carrier operators by algorithm:
        // 0: Op1 is carrier
        // 1: Op1, Op2 are carriers
        // 2: Op1, Op3 are carriers
        // 3: Op1, Op2 are carriers
        // 4: Op1, Op3 are carriers
        // 5: Op1, Op2, Op3 are carriers
        // 6: Op1, Op2, Op3 are carriers
        // 7: All are carriers

        bool[] carriers = Algorithm switch
        {
            0 => new[] { true, false, false, false },
            1 => new[] { true, true, false, false },
            2 => new[] { true, false, true, false },
            3 => new[] { true, true, false, false },
            4 => new[] { true, false, true, false },
            5 => new[] { true, true, true, false },
            6 => new[] { true, true, true, false },
            7 => new[] { true, true, true, true },
            _ => new[] { true, false, false, false }
        };

        for (int i = 0; i < Operators.Count && i < 4; i++)
        {
            Operators[i].IsCarrier = carriers[i];
        }
    }

    /// <summary>
    /// Applies an operator parameter to the synth.
    /// </summary>
    internal void ApplyOperatorParameter(int opIndex, string param, int value)
    {
        if (_synth == null) return;
        _synth.SetParameter($"ch{ActiveChannel}_op{opIndex}_{param}", value);
    }

    partial void OnAlgorithmChanged(int value)
    {
        if (_synth != null && ActiveChannel >= 0 && ActiveChannel < 6)
        {
            _synth.Channels[ActiveChannel].Algorithm = (OPNAlgorithm)value;
        }
        UpdateCarrierStatus();
        OnPropertyChanged(nameof(AlgorithmDescription));
        OnPropertyChanged(nameof(AlgorithmShortDescription));
    }

    partial void OnFeedbackChanged(int value)
    {
        if (_synth != null && ActiveChannel >= 0 && ActiveChannel < 6)
        {
            _synth.Channels[ActiveChannel].Feedback = Math.Clamp(value, 0, 7);
        }
    }

    partial void OnMasterVolumeChanged(float value)
    {
        if (_synth != null)
        {
            _synth.Volume = value;
        }
    }

    partial void OnActiveChannelChanged(int value)
    {
        if (_synth != null)
        {
            _synth.ActiveChannel = value;
        }
        LoadFromChannel();
        UpdateCarrierStatus();
    }

    partial void OnLfoEnabledChanged(bool value)
    {
        if (_synth != null)
        {
            _synth.LfoEnabled = value;
        }
    }

    partial void OnLfoFrequencyChanged(int value)
    {
        if (_synth != null)
        {
            _synth.LfoFrequency = Math.Clamp(value, 0, 7);
        }
        OnPropertyChanged(nameof(LfoFrequencyDisplay));
    }

    partial void OnPmSensitivityChanged(int value)
    {
        if (_synth != null && ActiveChannel >= 0 && ActiveChannel < 6)
        {
            _synth.Channels[ActiveChannel].FmSensitivity = Math.Clamp(value, 0, 7);
        }
    }

    partial void OnAmSensitivityChanged(int value)
    {
        if (_synth != null && ActiveChannel >= 0 && ActiveChannel < 6)
        {
            _synth.Channels[ActiveChannel].AmSensitivity = Math.Clamp(value, 0, 3);
        }
    }

    partial void OnPanLeftChanged(bool value)
    {
        if (value && _synth != null && ActiveChannel >= 0 && ActiveChannel < 6)
        {
            _synth.Channels[ActiveChannel].Panning = 2;
            PanCenter = false;
            PanRight = false;
        }
    }

    partial void OnPanCenterChanged(bool value)
    {
        if (value && _synth != null && ActiveChannel >= 0 && ActiveChannel < 6)
        {
            _synth.Channels[ActiveChannel].Panning = 3;
            PanLeft = false;
            PanRight = false;
        }
    }

    partial void OnPanRightChanged(bool value)
    {
        if (value && _synth != null && ActiveChannel >= 0 && ActiveChannel < 6)
        {
            _synth.Channels[ActiveChannel].Panning = 1;
            PanLeft = false;
            PanCenter = false;
        }
    }

    partial void OnSelectedOperatorChanged(OPNOperatorViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedOperatorName));
        OnPropertyChanged(nameof(SelectedOperatorColor));
    }

    [RelayCommand]
    private void SelectOperator(OPNOperatorViewModel? op)
    {
        SelectedOperator = op;
    }

    [RelayCommand]
    private void SetAlgorithm(string algStr)
    {
        if (int.TryParse(algStr, out int alg))
        {
            Algorithm = Math.Clamp(alg, 0, 7);
        }
    }

    [RelayCommand]
    private void LoadPreset(string presetName)
    {
        OPNSynth? newSynth = presetName.ToLowerInvariant() switch
        {
            "soniclead" or "sonic lead" => OPNSynth.CreateSonicLead(),
            "genesisbass" or "genesis bass" => OPNSynth.CreateGenesisBass(),
            "fmpiano" or "fm piano" => OPNSynth.CreateFMPiano(),
            "brass" => OPNSynth.CreateBrass(),
            "bell" => CreateBellPreset(),
            "strings" => CreateStringsPreset(),
            "organ" => CreateOrganPreset(),
            "slapbass" or "slap bass" => CreateSlapBassPreset(),
            "sfxhit" or "sfx hit" => CreateSFXHitPreset(),
            "laser" => CreateLaserPreset(),
            "powerup" or "power up" => CreatePowerUpPreset(),
            _ => new OPNSynth()
        };

        if (newSynth != null)
        {
            _synth = newSynth;
            LoadFromSynth();
            PresetName = _synth.Name;
            StatusMessage = $"Loaded preset: {PresetName}";
        }
    }

    [RelayCommand]
    private void InitPatch()
    {
        if (_synth == null)
        {
            Initialize();
        }
        else
        {
            _synth = new OPNSynth();
            LoadFromSynth();
        }
        PresetName = "Init";
        StatusMessage = "Initialized patch";
    }

    [RelayCommand]
    private void RandomizePatch()
    {
        if (_synth == null) return;

        var random = new Random();

        // Randomize algorithm
        Algorithm = random.Next(8);
        Feedback = random.Next(8);

        // Randomize operators
        foreach (var op in Operators)
        {
            op.TotalLevel = random.Next(80);
            op.Multiple = random.Next(16);
            op.Detune = random.Next(8);
            op.AttackRate = random.Next(20, 32);
            op.Decay1Rate = random.Next(32);
            op.Decay2Rate = random.Next(32);
            op.SustainLevel = random.Next(16);
            op.ReleaseRate = random.Next(16);
        }

        PresetName = "Random";
        StatusMessage = "Randomized patch";
    }

    [RelayCommand]
    private void ResetSelectedOperator()
    {
        SelectedOperator?.Reset();
        StatusMessage = $"Reset {SelectedOperator?.DisplayName ?? "operator"}";
    }

    [RelayCommand]
    private void CopyToAllOperators()
    {
        if (SelectedOperator == null) return;

        foreach (var op in Operators)
        {
            if (op == SelectedOperator) continue;

            op.TotalLevel = SelectedOperator.TotalLevel;
            op.Multiple = SelectedOperator.Multiple;
            op.Detune = SelectedOperator.Detune;
            op.KeyScale = SelectedOperator.KeyScale;
            op.AttackRate = SelectedOperator.AttackRate;
            op.Decay1Rate = SelectedOperator.Decay1Rate;
            op.Decay2Rate = SelectedOperator.Decay2Rate;
            op.SustainLevel = SelectedOperator.SustainLevel;
            op.ReleaseRate = SelectedOperator.ReleaseRate;
            op.SsgEgEnabled = SelectedOperator.SsgEgEnabled;
        }

        StatusMessage = $"Copied {SelectedOperator.DisplayName} settings to all operators";
    }

    [RelayCommand]
    private void SetMultiplePreset(string mulStr)
    {
        if (SelectedOperator != null && int.TryParse(mulStr, out int mul))
        {
            SelectedOperator.Multiple = Math.Clamp(mul, 0, 15);
        }
    }

    #region Additional Presets

    private static OPNSynth CreateBellPreset()
    {
        var synth = new OPNSynth { Name = "Bell" };
        var ch = synth.Channels[0];
        ch.Algorithm = OPNAlgorithm.Algo4;
        ch.Feedback = 0;

        ch.Operators[0].Multiple = 1;
        ch.Operators[0].TotalLevel = 20;
        ch.Operators[0].AttackRate = 31;
        ch.Operators[0].Decay1Rate = 8;
        ch.Operators[0].SustainLevel = 0;
        ch.Operators[0].ReleaseRate = 5;

        ch.Operators[1].Multiple = 14;
        ch.Operators[1].TotalLevel = 45;
        ch.Operators[1].AttackRate = 31;
        ch.Operators[1].Decay1Rate = 5;

        ch.Operators[2].Multiple = 1;
        ch.Operators[2].TotalLevel = 25;
        ch.Operators[2].AttackRate = 31;
        ch.Operators[2].Decay1Rate = 8;
        ch.Operators[2].SustainLevel = 0;
        ch.Operators[2].ReleaseRate = 5;

        ch.Operators[3].Multiple = 7;
        ch.Operators[3].TotalLevel = 50;
        ch.Operators[3].AttackRate = 31;
        ch.Operators[3].Decay1Rate = 4;

        return synth;
    }

    private static OPNSynth CreateStringsPreset()
    {
        var synth = new OPNSynth { Name = "FM Strings" };
        var ch = synth.Channels[0];
        ch.Algorithm = OPNAlgorithm.Algo5;
        ch.Feedback = 2;

        for (int i = 0; i < 4; i++)
        {
            ch.Operators[i].Multiple = i == 3 ? 2 : 1;
            ch.Operators[i].TotalLevel = i == 3 ? 50 : 30;
            ch.Operators[i].AttackRate = 20;
            ch.Operators[i].Decay1Rate = 5;
            ch.Operators[i].Decay2Rate = 2;
            ch.Operators[i].SustainLevel = 10;
            ch.Operators[i].ReleaseRate = 5;
        }

        return synth;
    }

    private static OPNSynth CreateOrganPreset()
    {
        var synth = new OPNSynth { Name = "FM Organ" };
        var ch = synth.Channels[0];
        ch.Algorithm = OPNAlgorithm.Parallel;
        ch.Feedback = 3;

        ch.Operators[0].Multiple = 1;
        ch.Operators[0].TotalLevel = 25;
        ch.Operators[0].AttackRate = 31;
        ch.Operators[0].SustainLevel = 15;

        ch.Operators[1].Multiple = 2;
        ch.Operators[1].TotalLevel = 30;
        ch.Operators[1].AttackRate = 31;
        ch.Operators[1].SustainLevel = 15;

        ch.Operators[2].Multiple = 4;
        ch.Operators[2].TotalLevel = 35;
        ch.Operators[2].AttackRate = 31;
        ch.Operators[2].SustainLevel = 15;

        ch.Operators[3].Multiple = 8;
        ch.Operators[3].TotalLevel = 40;
        ch.Operators[3].AttackRate = 31;
        ch.Operators[3].SustainLevel = 15;

        return synth;
    }

    private static OPNSynth CreateSlapBassPreset()
    {
        var synth = new OPNSynth { Name = "Slap Bass" };
        var ch = synth.Channels[0];
        ch.Algorithm = OPNAlgorithm.Serial;
        ch.Feedback = 5;

        ch.Operators[0].Multiple = 1;
        ch.Operators[0].TotalLevel = 15;
        ch.Operators[0].AttackRate = 31;
        ch.Operators[0].Decay1Rate = 12;
        ch.Operators[0].SustainLevel = 3;
        ch.Operators[0].ReleaseRate = 8;

        ch.Operators[1].Multiple = 1;
        ch.Operators[1].TotalLevel = 25;
        ch.Operators[1].AttackRate = 31;
        ch.Operators[1].Decay1Rate = 8;

        ch.Operators[2].Multiple = 5;
        ch.Operators[2].TotalLevel = 60;
        ch.Operators[2].AttackRate = 31;
        ch.Operators[2].Decay1Rate = 6;

        ch.Operators[3].Multiple = 1;
        ch.Operators[3].TotalLevel = 40;
        ch.Operators[3].AttackRate = 31;
        ch.Operators[3].Decay1Rate = 10;

        return synth;
    }

    private static OPNSynth CreateSFXHitPreset()
    {
        var synth = new OPNSynth { Name = "SFX Hit" };
        var ch = synth.Channels[0];
        ch.Algorithm = OPNAlgorithm.Serial;
        ch.Feedback = 7;

        ch.Operators[0].Multiple = 0;
        ch.Operators[0].TotalLevel = 10;
        ch.Operators[0].AttackRate = 31;
        ch.Operators[0].Decay1Rate = 20;
        ch.Operators[0].SustainLevel = 0;
        ch.Operators[0].ReleaseRate = 15;

        ch.Operators[1].Multiple = 3;
        ch.Operators[1].TotalLevel = 20;
        ch.Operators[1].AttackRate = 31;
        ch.Operators[1].Decay1Rate = 15;

        ch.Operators[2].Multiple = 7;
        ch.Operators[2].TotalLevel = 35;
        ch.Operators[2].AttackRate = 31;
        ch.Operators[2].Decay1Rate = 10;

        ch.Operators[3].Multiple = 11;
        ch.Operators[3].TotalLevel = 50;
        ch.Operators[3].AttackRate = 31;
        ch.Operators[3].Decay1Rate = 8;

        return synth;
    }

    private static OPNSynth CreateLaserPreset()
    {
        var synth = new OPNSynth { Name = "Laser" };
        var ch = synth.Channels[0];
        ch.Algorithm = OPNAlgorithm.Algo1;
        ch.Feedback = 6;

        ch.Operators[0].Multiple = 1;
        ch.Operators[0].TotalLevel = 15;
        ch.Operators[0].Detune = 3;
        ch.Operators[0].AttackRate = 31;
        ch.Operators[0].Decay1Rate = 25;
        ch.Operators[0].SustainLevel = 0;
        ch.Operators[0].ReleaseRate = 10;

        ch.Operators[1].Multiple = 1;
        ch.Operators[1].TotalLevel = 20;
        ch.Operators[1].Detune = 5;
        ch.Operators[1].AttackRate = 31;
        ch.Operators[1].Decay1Rate = 20;

        ch.Operators[2].Multiple = 4;
        ch.Operators[2].TotalLevel = 40;
        ch.Operators[2].AttackRate = 31;
        ch.Operators[2].Decay1Rate = 15;

        ch.Operators[3].Multiple = 8;
        ch.Operators[3].TotalLevel = 55;
        ch.Operators[3].AttackRate = 31;
        ch.Operators[3].Decay1Rate = 12;

        return synth;
    }

    private static OPNSynth CreatePowerUpPreset()
    {
        var synth = new OPNSynth { Name = "Power Up" };
        synth.LfoEnabled = true;
        synth.LfoFrequency = 4;

        var ch = synth.Channels[0];
        ch.Algorithm = OPNAlgorithm.Algo4;
        ch.Feedback = 4;
        ch.FmSensitivity = 3;

        ch.Operators[0].Multiple = 2;
        ch.Operators[0].TotalLevel = 20;
        ch.Operators[0].AttackRate = 28;
        ch.Operators[0].Decay1Rate = 6;
        ch.Operators[0].SustainLevel = 8;
        ch.Operators[0].ReleaseRate = 6;

        ch.Operators[1].Multiple = 3;
        ch.Operators[1].TotalLevel = 35;
        ch.Operators[1].AttackRate = 31;
        ch.Operators[1].Decay1Rate = 8;

        ch.Operators[2].Multiple = 4;
        ch.Operators[2].TotalLevel = 25;
        ch.Operators[2].AttackRate = 25;
        ch.Operators[2].Decay1Rate = 5;
        ch.Operators[2].SustainLevel = 10;
        ch.Operators[2].ReleaseRate = 5;

        ch.Operators[3].Multiple = 6;
        ch.Operators[3].TotalLevel = 45;
        ch.Operators[3].AttackRate = 31;
        ch.Operators[3].Decay1Rate = 6;

        return synth;
    }

    #endregion

    /// <summary>
    /// Gets the underlying OPNSynth instance.
    /// </summary>
    public OPNSynth? GetSynth() => _synth;

    /// <summary>
    /// Triggers a note on.
    /// </summary>
    public void NoteOn(int note, int velocity)
    {
        _synth?.NoteOn(note, velocity);
    }

    /// <summary>
    /// Triggers a note off.
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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        AllNotesOff();
        GC.SuppressFinalize(this);
    }
}

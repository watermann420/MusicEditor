// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Electric Piano Synthesizer Editor.

using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MusicEngine.Core.Synthesizers;
using MusicEngineEditor.ViewModels;

namespace MusicEngineEditor.ViewModels.Synths;

/// <summary>
/// ViewModel for the Electric Piano Synthesizer Editor control.
/// Provides data binding and commands for the EPianoSynthControl.
/// </summary>
public partial class EPianoSynthViewModel : ViewModelBase, IDisposable
{
    private EPianoSynth? _synth;
    private bool _disposed;

    #region Observable Properties - Synth Parameters

    [ObservableProperty]
    private float _volume = 0.5f;

    [ObservableProperty]
    private float _tineBarMix = 0.5f;

    [ObservableProperty]
    private float _bellAmount = 0.3f;

    [ObservableProperty]
    private float _barkAmount = 0.3f;

    [ObservableProperty]
    private float _decayTime = 3.0f;

    [ObservableProperty]
    private float _drive;

    [ObservableProperty]
    private float _bass = 0.5f;

    [ObservableProperty]
    private float _treble = 0.5f;

    #endregion

    #region Observable Properties - Tremolo

    [ObservableProperty]
    private bool _tremoloEnabled;

    [ObservableProperty]
    private float _tremoloRate = 5.5f;

    [ObservableProperty]
    private float _tremoloDepth = 0.5f;

    [ObservableProperty]
    private float _stereoWidth = 0.3f;

    #endregion

    #region Observable Properties - Chorus

    [ObservableProperty]
    private bool _chorusEnabled;

    [ObservableProperty]
    private float _chorusRate = 0.8f;

    [ObservableProperty]
    private float _chorusDepth = 0.5f;

    [ObservableProperty]
    private float _chorusMix = 0.5f;

    #endregion

    #region Observable Properties - Phaser

    [ObservableProperty]
    private bool _phaserEnabled;

    [ObservableProperty]
    private float _phaserRate = 0.5f;

    [ObservableProperty]
    private float _phaserDepth = 0.7f;

    [ObservableProperty]
    private float _phaserFeedback = 0.5f;

    [ObservableProperty]
    private float _phaserMix = 0.5f;

    #endregion

    #region Observable Properties - Model Selection

    [ObservableProperty]
    private EPianoModel _selectedModel = EPianoModel.RhodesMarkI;

    [ObservableProperty]
    private string _selectedModelName = "Rhodes Mark I";

    [ObservableProperty]
    private string _modelDescription = "Warm, bell-like tone with characteristic bark on hard hits.";

    [ObservableProperty]
    private bool _isRhodesMarkI = true;

    [ObservableProperty]
    private bool _isRhodesMarkII;

    [ObservableProperty]
    private bool _isRhodesSuitcase;

    [ObservableProperty]
    private bool _isWurlitzer;

    [ObservableProperty]
    private bool _isElectricGrand;

    #endregion

    #region Observable Properties - UI State

    [ObservableProperty]
    private int _activeVoices;

    [ObservableProperty]
    private int _maxVoices = 16;

    [ObservableProperty]
    private bool _isPlaying;

    #endregion

    private static readonly Dictionary<EPianoModel, string> ModelNames = new()
    {
        { EPianoModel.RhodesMarkI, "Rhodes Mark I" },
        { EPianoModel.RhodesMarkII, "Rhodes Mark II" },
        { EPianoModel.RhodesSuitcase, "Rhodes Suitcase" },
        { EPianoModel.Wurlitzer, "Wurlitzer 200A" },
        { EPianoModel.ElectricGrand, "Electric Grand" }
    };

    private static readonly Dictionary<EPianoModel, string> ModelDescriptions = new()
    {
        { EPianoModel.RhodesMarkI, "Warm, bell-like tone with characteristic bark on hard hits. The classic electric piano sound." },
        { EPianoModel.RhodesMarkII, "Brighter and more aggressive than Mark I, with enhanced bark and punch." },
        { EPianoModel.RhodesSuitcase, "Built-in stereo tremolo for that lush, swirling sound. Slightly warmer than stage models." },
        { EPianoModel.Wurlitzer, "Reedy, growly tone with more bite. Famous for its distinctive vibrato and bark." },
        { EPianoModel.ElectricGrand, "Combines hammer action with electric pickups. More piano-like attack with electric sustain." }
    };

    public EPianoSynthViewModel()
    {
    }

    public EPianoSynthViewModel(EPianoSynth synth)
    {
        _synth = synth ?? throw new ArgumentNullException(nameof(synth));
        LoadFromSynth();
    }

    /// <summary>
    /// Initializes or sets the synth instance.
    /// </summary>
    public void Initialize(EPianoSynth? synth = null)
    {
        _synth = synth ?? new EPianoSynth();
        LoadFromSynth();
    }

    private void LoadFromSynth()
    {
        if (_synth == null) return;

        Volume = _synth.Volume;
        SelectedModel = _synth.Model;
        UpdateModelSelection(_synth.Model);

        TremoloEnabled = _synth.TremoloEnabled;
        TremoloRate = _synth.TremoloRate;
        TremoloDepth = _synth.TremoloDepth;
        StereoWidth = _synth.TremoloStereo;

        ChorusEnabled = _synth.ChorusEnabled;
        ChorusRate = _synth.ChorusRate;
        ChorusDepth = _synth.ChorusDepth;
        ChorusMix = _synth.ChorusMix;

        PhaserEnabled = _synth.PhaserEnabled;
        PhaserRate = _synth.PhaserRate;
        PhaserDepth = _synth.PhaserDepth;
        PhaserFeedback = _synth.PhaserFeedback;
        PhaserMix = _synth.PhaserMix;

        Bass = _synth.Bass;
        Treble = _synth.Treble;
        Drive = _synth.Drive;

        MaxVoices = _synth.MaxVoices;
    }

    private void UpdateModelSelection(EPianoModel model)
    {
        IsRhodesMarkI = model == EPianoModel.RhodesMarkI;
        IsRhodesMarkII = model == EPianoModel.RhodesMarkII;
        IsRhodesSuitcase = model == EPianoModel.RhodesSuitcase;
        IsWurlitzer = model == EPianoModel.Wurlitzer;
        IsElectricGrand = model == EPianoModel.ElectricGrand;

        SelectedModelName = ModelNames.GetValueOrDefault(model, "Unknown");
        ModelDescription = ModelDescriptions.GetValueOrDefault(model, "");

        // Update default parameters based on model
        switch (model)
        {
            case EPianoModel.RhodesMarkI:
                TineBarMix = 0.6f;
                BellAmount = 0.3f;
                BarkAmount = 0.3f;
                break;
            case EPianoModel.RhodesMarkII:
                TineBarMix = 0.7f;
                BellAmount = 0.35f;
                BarkAmount = 0.5f;
                break;
            case EPianoModel.RhodesSuitcase:
                TineBarMix = 0.55f;
                BellAmount = 0.25f;
                BarkAmount = 0.2f;
                if (!TremoloEnabled)
                {
                    TremoloEnabled = true;
                    TremoloRate = 5.5f;
                    TremoloDepth = 0.6f;
                    StereoWidth = 0.5f;
                }
                break;
            case EPianoModel.Wurlitzer:
                TineBarMix = 0.8f;
                BellAmount = 0.2f;
                BarkAmount = 0.7f;
                break;
            case EPianoModel.ElectricGrand:
                TineBarMix = 0.4f;
                BellAmount = 0.15f;
                BarkAmount = 0.1f;
                break;
        }
    }

    #region Property Change Handlers

    partial void OnVolumeChanged(float value)
    {
        if (_synth != null)
            _synth.Volume = value;
    }

    partial void OnSelectedModelChanged(EPianoModel value)
    {
        if (_synth != null)
            _synth.Model = value;
        UpdateModelSelection(value);
    }

    partial void OnTremoloEnabledChanged(bool value)
    {
        if (_synth != null)
            _synth.TremoloEnabled = value;
    }

    partial void OnTremoloRateChanged(float value)
    {
        if (_synth != null)
            _synth.TremoloRate = value;
    }

    partial void OnTremoloDepthChanged(float value)
    {
        if (_synth != null)
            _synth.TremoloDepth = value;
    }

    partial void OnStereoWidthChanged(float value)
    {
        if (_synth != null)
            _synth.TremoloStereo = value;
    }

    partial void OnChorusEnabledChanged(bool value)
    {
        if (_synth != null)
            _synth.ChorusEnabled = value;
    }

    partial void OnChorusRateChanged(float value)
    {
        if (_synth != null)
            _synth.ChorusRate = value;
    }

    partial void OnChorusDepthChanged(float value)
    {
        if (_synth != null)
            _synth.ChorusDepth = value;
    }

    partial void OnChorusMixChanged(float value)
    {
        if (_synth != null)
            _synth.ChorusMix = value;
    }

    partial void OnPhaserEnabledChanged(bool value)
    {
        if (_synth != null)
            _synth.PhaserEnabled = value;
    }

    partial void OnPhaserRateChanged(float value)
    {
        if (_synth != null)
            _synth.PhaserRate = value;
    }

    partial void OnPhaserDepthChanged(float value)
    {
        if (_synth != null)
            _synth.PhaserDepth = value;
    }

    partial void OnPhaserFeedbackChanged(float value)
    {
        if (_synth != null)
            _synth.PhaserFeedback = value;
    }

    partial void OnPhaserMixChanged(float value)
    {
        if (_synth != null)
            _synth.PhaserMix = value;
    }

    partial void OnBassChanged(float value)
    {
        if (_synth != null)
            _synth.Bass = value;
    }

    partial void OnTrebleChanged(float value)
    {
        if (_synth != null)
            _synth.Treble = value;
    }

    partial void OnDriveChanged(float value)
    {
        if (_synth != null)
            _synth.Drive = value;
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void SelectModel(string modelName)
    {
        if (Enum.TryParse<EPianoModel>(modelName, out var model))
        {
            SelectedModel = model;
            StatusMessage = $"Selected model: {ModelNames.GetValueOrDefault(model, modelName)}";
        }
    }

    [RelayCommand]
    private void ResetParameters()
    {
        Volume = 0.5f;
        TineBarMix = 0.5f;
        BellAmount = 0.3f;
        BarkAmount = 0.3f;
        DecayTime = 3.0f;
        Drive = 0f;
        Bass = 0.5f;
        Treble = 0.5f;

        TremoloEnabled = false;
        TremoloRate = 5.5f;
        TremoloDepth = 0.5f;
        StereoWidth = 0.3f;

        ChorusEnabled = false;
        ChorusRate = 0.8f;
        ChorusDepth = 0.5f;
        ChorusMix = 0.5f;

        PhaserEnabled = false;
        PhaserRate = 0.5f;
        PhaserDepth = 0.7f;
        PhaserFeedback = 0.5f;
        PhaserMix = 0.5f;

        SelectedModel = EPianoModel.RhodesMarkI;

        StatusMessage = "Parameters reset to defaults";
    }

    [RelayCommand]
    private void LoadClassicPreset()
    {
        SelectedModel = EPianoModel.RhodesMarkI;
        TremoloEnabled = false;
        ChorusEnabled = true;
        ChorusRate = 0.6f;
        ChorusDepth = 0.3f;
        ChorusMix = 0.3f;
        PhaserEnabled = false;
        Drive = 0f;

        StatusMessage = "Loaded: Rhodes Classic preset";
    }

    [RelayCommand]
    private void LoadSuitcasePreset()
    {
        SelectedModel = EPianoModel.RhodesSuitcase;
        TremoloEnabled = true;
        TremoloRate = 5.5f;
        TremoloDepth = 0.6f;
        StereoWidth = 0.5f;
        ChorusEnabled = false;
        PhaserEnabled = false;
        Drive = 0f;

        StatusMessage = "Loaded: Rhodes Suitcase preset";
    }

    [RelayCommand]
    private void LoadWurlitzerPreset()
    {
        SelectedModel = EPianoModel.Wurlitzer;
        TremoloEnabled = true;
        TremoloRate = 6f;
        TremoloDepth = 0.4f;
        StereoWidth = 0.2f;
        ChorusEnabled = false;
        PhaserEnabled = false;
        Drive = 0.1f;

        StatusMessage = "Loaded: Wurlitzer preset";
    }

    [RelayCommand]
    private void LoadBalladPreset()
    {
        SelectedModel = EPianoModel.RhodesMarkI;
        Treble = 0.4f;
        Bass = 0.55f;
        TremoloEnabled = false;
        ChorusEnabled = true;
        ChorusRate = 0.3f;
        ChorusDepth = 0.4f;
        ChorusMix = 0.4f;
        PhaserEnabled = false;
        Drive = 0f;

        StatusMessage = "Loaded: Ballad Rhodes preset";
    }

    [RelayCommand]
    private void LoadDrivenPreset()
    {
        SelectedModel = EPianoModel.RhodesMarkII;
        Drive = 0.4f;
        TremoloEnabled = false;
        ChorusEnabled = false;
        PhaserEnabled = true;
        PhaserRate = 0.4f;
        PhaserDepth = 0.6f;
        PhaserFeedback = 0.3f;
        PhaserMix = 0.4f;

        StatusMessage = "Loaded: Driven Rhodes preset";
    }

    [RelayCommand]
    private void LoadGrandPreset()
    {
        SelectedModel = EPianoModel.ElectricGrand;
        TremoloEnabled = false;
        ChorusEnabled = true;
        ChorusRate = 0.5f;
        ChorusDepth = 0.2f;
        ChorusMix = 0.2f;
        PhaserEnabled = false;
        Drive = 0f;

        StatusMessage = "Loaded: Electric Grand preset";
    }

    [RelayCommand]
    private void PlayNote(int note)
    {
        if (_synth == null) return;

        _synth.NoteOn(note, 100);
        IsPlaying = true;
        ActiveVoices++;
    }

    [RelayCommand]
    private void StopNote(int note)
    {
        if (_synth == null) return;

        _synth.NoteOff(note);
        ActiveVoices = Math.Max(0, ActiveVoices - 1);
        if (ActiveVoices == 0)
            IsPlaying = false;
    }

    [RelayCommand]
    private void StopAllNotes()
    {
        _synth?.AllNotesOff();
        IsPlaying = false;
        ActiveVoices = 0;
    }

    #endregion

    /// <summary>
    /// Gets the underlying EPianoSynth instance.
    /// </summary>
    public EPianoSynth? GetSynth() => _synth;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        StopAllNotes();
        // Note: We don't dispose the synth here as it may be owned externally
    }
}

// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the GlitchMachine effect control with randomization,
// pattern sequencing, and multiple glitch effect modules.

using System;
using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Effects;

#region Supporting Types

/// <summary>
/// Represents a single glitch effect module.
/// </summary>
public partial class GlitchEffectModule : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private GlitchEffectType _effectType;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private float _amount = 0.5f;

    [ObservableProperty]
    private float _parameter1;

    [ObservableProperty]
    private string _parameter1Name = "Param 1";

    [ObservableProperty]
    private float _parameter2;

    [ObservableProperty]
    private string _parameter2Name = "Param 2";

    /// <summary>
    /// Event raised when enabled state changes.
    /// </summary>
    public event EventHandler<bool>? EnabledChanged;

    partial void OnIsEnabledChanged(bool value)
    {
        EnabledChanged?.Invoke(this, value);
    }
}

/// <summary>
/// Types of glitch effects available.
/// </summary>
public enum GlitchEffectType
{
    BufferRepeat,
    TapeStop,
    BitReduction,
    SampleRateReduction,
    Reverse,
    Stretch,
    Gate,
    FilterSweep
}

/// <summary>
/// Represents a step in the glitch pattern sequencer.
/// </summary>
public partial class GlitchPatternStep : ObservableObject
{
    [ObservableProperty]
    private int _stepIndex;

    [ObservableProperty]
    private GlitchEffectType? _selectedEffect;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isCurrent;

    [ObservableProperty]
    private float _intensity = 1.0f;

    /// <summary>
    /// Event raised when the step configuration changes.
    /// </summary>
    public event EventHandler? StepChanged;

    partial void OnSelectedEffectChanged(GlitchEffectType? value)
    {
        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIsActiveChanged(bool value)
    {
        StepChanged?.Invoke(this, EventArgs.Empty);
    }

    partial void OnIntensityChanged(float value)
    {
        StepChanged?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// Glitch style presets.
/// </summary>
public enum GlitchPreset
{
    Subtle,
    Moderate,
    Extreme,
    Rhythmic,
    Chaotic,
    Retro,
    Digital,
    Tape
}

#endregion

/// <summary>
/// ViewModel for the GlitchMachine effect control providing comprehensive
/// glitch effect management with randomization and pattern sequencing.
/// </summary>
public partial class GlitchMachineViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private readonly Random _random = new();
    private System.Timers.Timer? _glitchTimer;
    private System.Timers.Timer? _waveformTimer;
    private int _currentStepIndex;
    private float[] _inputWaveform = new float[256];
    private float[] _outputWaveform = new float[256];

    #region Observable Properties - Main Controls

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private bool _isBypassed;

    [ObservableProperty]
    private float _mix = 0.5f;

    [ObservableProperty]
    private bool _syncToTempo = true;

    [ObservableProperty]
    private float _tempo = 120f;

    #endregion

    #region Observable Properties - Randomization

    [ObservableProperty]
    private float _chaosAmount = 0.3f;

    [ObservableProperty]
    private float _triggerRate = 4f;

    [ObservableProperty]
    private float _durationMin = 50f;

    [ObservableProperty]
    private float _durationMax = 200f;

    #endregion

    #region Observable Properties - Display

    [ObservableProperty]
    private string _activeGlitchName = "None";

    [ObservableProperty]
    private bool _isGlitchActive;

    [ObservableProperty]
    private float _glitchProgress;

    [ObservableProperty]
    private int _glitchesTriggered;

    [ObservableProperty]
    private GlitchPreset _selectedPreset = GlitchPreset.Moderate;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the collection of effect modules.
    /// </summary>
    public ObservableCollection<GlitchEffectModule> EffectModules { get; } = new();

    /// <summary>
    /// Gets the pattern sequencer steps.
    /// </summary>
    public ObservableCollection<GlitchPatternStep> PatternSteps { get; } = new();

    /// <summary>
    /// Gets the available presets.
    /// </summary>
    public ObservableCollection<GlitchPreset> AvailablePresets { get; } = new(Enum.GetValues<GlitchPreset>());

    /// <summary>
    /// Gets the available effect types for the pattern sequencer.
    /// </summary>
    public ObservableCollection<GlitchEffectType> AvailableEffectTypes { get; } = new(Enum.GetValues<GlitchEffectType>());

    /// <summary>
    /// Gets the input waveform data for display.
    /// </summary>
    public float[] InputWaveform => _inputWaveform;

    /// <summary>
    /// Gets the output waveform data for display.
    /// </summary>
    public float[] OutputWaveform => _outputWaveform;

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a glitch is triggered.
    /// </summary>
    public event EventHandler<GlitchEffectType>? GlitchTriggered;

    /// <summary>
    /// Event raised when a glitch completes.
    /// </summary>
    public event EventHandler<GlitchEffectType>? GlitchCompleted;

    /// <summary>
    /// Event raised when a parameter changes.
    /// </summary>
    public event EventHandler<string>? ParameterChanged;

    /// <summary>
    /// Event raised when waveform data updates.
    /// </summary>
    public event EventHandler? WaveformUpdated;

    /// <summary>
    /// Event raised when the pattern step changes.
    /// </summary>
    public event EventHandler<int>? PatternStepChanged;

    #endregion

    #region Constructor

    public GlitchMachineViewModel()
    {
        InitializeEffectModules();
        InitializePatternSteps();
        StartTimers();
    }

    #endregion

    #region Initialization

    private void InitializeEffectModules()
    {
        EffectModules.Add(new GlitchEffectModule
        {
            Name = "Buffer Repeat",
            Description = "Repeat/stutter the audio buffer",
            EffectType = GlitchEffectType.BufferRepeat,
            Parameter1Name = "Repeat Count",
            Parameter1 = 4f,
            Parameter2Name = "Decay",
            Parameter2 = 0.9f
        });

        EffectModules.Add(new GlitchEffectModule
        {
            Name = "Tape Stop",
            Description = "Simulate tape stop/start effect",
            EffectType = GlitchEffectType.TapeStop,
            Parameter1Name = "Stop Time",
            Parameter1 = 0.5f,
            Parameter2Name = "Curve",
            Parameter2 = 0.7f
        });

        EffectModules.Add(new GlitchEffectModule
        {
            Name = "Bit Crush",
            Description = "Reduce bit depth for lo-fi effect",
            EffectType = GlitchEffectType.BitReduction,
            Parameter1Name = "Bit Depth",
            Parameter1 = 8f,
            Parameter2Name = "Dither",
            Parameter2 = 0.1f
        });

        EffectModules.Add(new GlitchEffectModule
        {
            Name = "Sample Rate",
            Description = "Reduce sample rate for aliasing",
            EffectType = GlitchEffectType.SampleRateReduction,
            Parameter1Name = "Rate Divisor",
            Parameter1 = 4f,
            Parameter2Name = "Smoothing",
            Parameter2 = 0f
        });

        EffectModules.Add(new GlitchEffectModule
        {
            Name = "Reverse",
            Description = "Reverse audio playback",
            EffectType = GlitchEffectType.Reverse,
            Parameter1Name = "Chunk Size",
            Parameter1 = 0.25f,
            Parameter2Name = "Crossfade",
            Parameter2 = 0.1f
        });

        EffectModules.Add(new GlitchEffectModule
        {
            Name = "Stretch",
            Description = "Time stretch/compress audio",
            EffectType = GlitchEffectType.Stretch,
            Parameter1Name = "Stretch Factor",
            Parameter1 = 0.5f,
            Parameter2Name = "Grain Size",
            Parameter2 = 0.05f
        });

        EffectModules.Add(new GlitchEffectModule
        {
            Name = "Gate/Chop",
            Description = "Rhythmic gating/chopping",
            EffectType = GlitchEffectType.Gate,
            Parameter1Name = "Rate",
            Parameter1 = 8f,
            Parameter2Name = "Shape",
            Parameter2 = 0.5f
        });

        EffectModules.Add(new GlitchEffectModule
        {
            Name = "Filter Sweep",
            Description = "Rapid filter frequency sweep",
            EffectType = GlitchEffectType.FilterSweep,
            Parameter1Name = "Sweep Range",
            Parameter1 = 0.8f,
            Parameter2Name = "Resonance",
            Parameter2 = 0.6f
        });

        foreach (var module in EffectModules)
        {
            module.EnabledChanged += (s, e) => ParameterChanged?.Invoke(this, $"{module.Name}.Enabled");
        }
    }

    private void InitializePatternSteps()
    {
        for (int i = 0; i < 8; i++)
        {
            var step = new GlitchPatternStep
            {
                StepIndex = i,
                IsActive = false,
                SelectedEffect = null,
                Intensity = 1.0f
            };
            step.StepChanged += (s, e) => ParameterChanged?.Invoke(this, $"Pattern.Step{((GlitchPatternStep?)s)?.StepIndex}");
            PatternSteps.Add(step);
        }
    }

    private void StartTimers()
    {
        // Glitch trigger timer
        _glitchTimer = new System.Timers.Timer(GetGlitchInterval());
        _glitchTimer.Elapsed += GlitchTimer_Elapsed;
        _glitchTimer.AutoReset = true;
        _glitchTimer.Start();

        // Waveform update timer
        _waveformTimer = new System.Timers.Timer(33); // ~30 FPS
        _waveformTimer.Elapsed += WaveformTimer_Elapsed;
        _waveformTimer.AutoReset = true;
        _waveformTimer.Start();
    }

    private double GetGlitchInterval()
    {
        if (SyncToTempo && Tempo > 0)
        {
            // Sync to beat divisions based on rate
            double beatMs = 60000.0 / Tempo;
            return beatMs / TriggerRate;
        }
        else
        {
            return 1000.0 / TriggerRate;
        }
    }

    #endregion

    #region Timer Handlers

    private void GlitchTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        if (!IsEnabled || IsBypassed) return;

        // Check if random glitch should trigger based on chaos amount
        if (_random.NextDouble() < ChaosAmount)
        {
            TriggerRandomGlitch();
        }

        // Advance pattern sequencer
        AdvancePatternStep();
    }

    private void WaveformTimer_Elapsed(object? sender, System.Timers.ElapsedEventArgs e)
    {
        try
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                UpdateWaveformVisualization();
                WaveformUpdated?.Invoke(this, EventArgs.Empty);
            });
        }
        catch
        {
            // Ignore dispatcher exceptions during shutdown
        }
    }

    #endregion

    #region Glitch Logic

    private void TriggerRandomGlitch()
    {
        // Get enabled effects
        var enabledEffects = new System.Collections.Generic.List<GlitchEffectModule>();
        foreach (var module in EffectModules)
        {
            if (module.IsEnabled)
            {
                enabledEffects.Add(module);
            }
        }

        if (enabledEffects.Count == 0) return;

        // Select random effect
        var selectedModule = enabledEffects[_random.Next(enabledEffects.Count)];
        TriggerGlitch(selectedModule.EffectType);
    }

    private void TriggerGlitch(GlitchEffectType effectType)
    {
        try
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsGlitchActive = true;
                ActiveGlitchName = GetEffectDisplayName(effectType);
                GlitchesTriggered++;
                GlitchProgress = 0;

                GlitchTriggered?.Invoke(this, effectType);

                // Calculate random duration
                float duration = DurationMin + (float)_random.NextDouble() * (DurationMax - DurationMin);

                // Schedule glitch completion
                var completionTimer = new System.Timers.Timer(duration);
                completionTimer.Elapsed += (s, e) =>
                {
                    completionTimer.Stop();
                    completionTimer.Dispose();
                    CompleteGlitch(effectType);
                };
                completionTimer.AutoReset = false;
                completionTimer.Start();
            });
        }
        catch
        {
            // Ignore dispatcher exceptions
        }
    }

    private void CompleteGlitch(GlitchEffectType effectType)
    {
        try
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                IsGlitchActive = false;
                ActiveGlitchName = "None";
                GlitchProgress = 0;

                GlitchCompleted?.Invoke(this, effectType);
            });
        }
        catch
        {
            // Ignore dispatcher exceptions
        }
    }

    private void AdvancePatternStep()
    {
        try
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                // Clear current indicator
                if (_currentStepIndex >= 0 && _currentStepIndex < PatternSteps.Count)
                {
                    PatternSteps[_currentStepIndex].IsCurrent = false;
                }

                // Advance to next step
                _currentStepIndex = (_currentStepIndex + 1) % PatternSteps.Count;

                // Set new current indicator
                PatternSteps[_currentStepIndex].IsCurrent = true;

                // Trigger effect if step is active
                var step = PatternSteps[_currentStepIndex];
                if (step.IsActive && step.SelectedEffect.HasValue)
                {
                    TriggerGlitch(step.SelectedEffect.Value);
                }

                PatternStepChanged?.Invoke(this, _currentStepIndex);
            });
        }
        catch
        {
            // Ignore dispatcher exceptions
        }
    }

    private void UpdateWaveformVisualization()
    {
        // Simulate input waveform (in real implementation, this would come from audio engine)
        for (int i = 0; i < _inputWaveform.Length; i++)
        {
            float t = (float)i / _inputWaveform.Length * MathF.PI * 8;
            _inputWaveform[i] = MathF.Sin(t + (float)DateTime.Now.Ticks / 10000000f) * 0.8f;
        }

        // Simulate output waveform with glitch effects applied
        Array.Copy(_inputWaveform, _outputWaveform, _inputWaveform.Length);

        if (IsGlitchActive)
        {
            ApplyGlitchVisualization();
        }

        // Apply mix
        for (int i = 0; i < _outputWaveform.Length; i++)
        {
            _outputWaveform[i] = _inputWaveform[i] * (1 - Mix) + _outputWaveform[i] * Mix;
        }
    }

    private void ApplyGlitchVisualization()
    {
        // Apply visual representation of the current glitch
        var effectType = GetEffectTypeFromName(ActiveGlitchName);

        switch (effectType)
        {
            case GlitchEffectType.BufferRepeat:
                // Stutter effect
                int repeatLength = _outputWaveform.Length / 8;
                for (int i = repeatLength; i < _outputWaveform.Length; i++)
                {
                    _outputWaveform[i] = _outputWaveform[i % repeatLength];
                }
                break;

            case GlitchEffectType.BitReduction:
                // Quantize values
                for (int i = 0; i < _outputWaveform.Length; i++)
                {
                    float steps = 16f;
                    _outputWaveform[i] = MathF.Round(_outputWaveform[i] * steps) / steps;
                }
                break;

            case GlitchEffectType.SampleRateReduction:
                // Hold values
                int holdLength = 4;
                for (int i = 0; i < _outputWaveform.Length; i++)
                {
                    _outputWaveform[i] = _outputWaveform[i / holdLength * holdLength];
                }
                break;

            case GlitchEffectType.Reverse:
                // Reverse sections
                int sectionLength = _outputWaveform.Length / 4;
                for (int s = 0; s < 4; s += 2)
                {
                    int start = s * sectionLength;
                    int end = start + sectionLength - 1;
                    while (start < end)
                    {
                        (_outputWaveform[start], _outputWaveform[end]) = (_outputWaveform[end], _outputWaveform[start]);
                        start++;
                        end--;
                    }
                }
                break;

            case GlitchEffectType.Gate:
                // Chop sections
                for (int i = 0; i < _outputWaveform.Length; i++)
                {
                    if ((i / 16) % 2 == 0)
                    {
                        _outputWaveform[i] = 0;
                    }
                }
                break;

            case GlitchEffectType.TapeStop:
                // Slow down/stretch
                for (int i = 0; i < _outputWaveform.Length; i++)
                {
                    float progress = (float)i / _outputWaveform.Length;
                    float slowdown = 1f - progress * 0.8f;
                    int sourceIndex = (int)(i * slowdown);
                    if (sourceIndex < _inputWaveform.Length)
                    {
                        _outputWaveform[i] = _inputWaveform[sourceIndex] * (1f - progress * 0.5f);
                    }
                }
                break;

            default:
                // Add some noise for other effects
                for (int i = 0; i < _outputWaveform.Length; i++)
                {
                    _outputWaveform[i] += (float)(_random.NextDouble() - 0.5) * 0.2f;
                }
                break;
        }
    }

    private string GetEffectDisplayName(GlitchEffectType effectType)
    {
        return effectType switch
        {
            GlitchEffectType.BufferRepeat => "Buffer Repeat",
            GlitchEffectType.TapeStop => "Tape Stop",
            GlitchEffectType.BitReduction => "Bit Crush",
            GlitchEffectType.SampleRateReduction => "Sample Rate",
            GlitchEffectType.Reverse => "Reverse",
            GlitchEffectType.Stretch => "Stretch",
            GlitchEffectType.Gate => "Gate/Chop",
            GlitchEffectType.FilterSweep => "Filter Sweep",
            _ => "Unknown"
        };
    }

    private GlitchEffectType GetEffectTypeFromName(string name)
    {
        return name switch
        {
            "Buffer Repeat" => GlitchEffectType.BufferRepeat,
            "Tape Stop" => GlitchEffectType.TapeStop,
            "Bit Crush" => GlitchEffectType.BitReduction,
            "Sample Rate" => GlitchEffectType.SampleRateReduction,
            "Reverse" => GlitchEffectType.Reverse,
            "Stretch" => GlitchEffectType.Stretch,
            "Gate/Chop" => GlitchEffectType.Gate,
            "Filter Sweep" => GlitchEffectType.FilterSweep,
            _ => GlitchEffectType.BufferRepeat
        };
    }

    #endregion

    #region Property Changed Handlers

    partial void OnTriggerRateChanged(float value)
    {
        if (_glitchTimer != null)
        {
            _glitchTimer.Interval = GetGlitchInterval();
        }
        ParameterChanged?.Invoke(this, nameof(TriggerRate));
    }

    partial void OnTempoChanged(float value)
    {
        if (_glitchTimer != null && SyncToTempo)
        {
            _glitchTimer.Interval = GetGlitchInterval();
        }
        ParameterChanged?.Invoke(this, nameof(Tempo));
    }

    partial void OnSyncToTempoChanged(bool value)
    {
        if (_glitchTimer != null)
        {
            _glitchTimer.Interval = GetGlitchInterval();
        }
        ParameterChanged?.Invoke(this, nameof(SyncToTempo));
    }

    partial void OnChaosAmountChanged(float value)
    {
        ParameterChanged?.Invoke(this, nameof(ChaosAmount));
    }

    partial void OnMixChanged(float value)
    {
        ParameterChanged?.Invoke(this, nameof(Mix));
    }

    partial void OnDurationMinChanged(float value)
    {
        if (value > DurationMax)
        {
            DurationMax = value;
        }
        ParameterChanged?.Invoke(this, nameof(DurationMin));
    }

    partial void OnDurationMaxChanged(float value)
    {
        if (value < DurationMin)
        {
            DurationMin = value;
        }
        ParameterChanged?.Invoke(this, nameof(DurationMax));
    }

    partial void OnSelectedPresetChanged(GlitchPreset value)
    {
        ApplyPresetInternal(value);
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void TriggerManualGlitch()
    {
        TriggerRandomGlitch();
    }

    [RelayCommand]
    private void TriggerSpecificGlitch(GlitchEffectType effectType)
    {
        TriggerGlitch(effectType);
    }

    [RelayCommand]
    private void StopAllGlitches()
    {
        IsGlitchActive = false;
        ActiveGlitchName = "None";
        GlitchProgress = 0;
    }

    [RelayCommand]
    private void RandomizePattern()
    {
        foreach (var step in PatternSteps)
        {
            step.IsActive = _random.NextDouble() > 0.5;
            if (step.IsActive)
            {
                var effects = Enum.GetValues<GlitchEffectType>();
                step.SelectedEffect = effects[_random.Next(effects.Length)];
                step.Intensity = 0.5f + (float)_random.NextDouble() * 0.5f;
            }
            else
            {
                step.SelectedEffect = null;
            }
        }
        StatusMessage = "Pattern randomized";
    }

    [RelayCommand]
    private void ClearPattern()
    {
        foreach (var step in PatternSteps)
        {
            step.IsActive = false;
            step.SelectedEffect = null;
            step.Intensity = 1.0f;
        }
        StatusMessage = "Pattern cleared";
    }

    [RelayCommand]
    private void ApplyPreset(GlitchPreset preset)
    {
        SelectedPreset = preset;
    }

    private void ApplyPresetInternal(GlitchPreset preset)
    {
        switch (preset)
        {
            case GlitchPreset.Subtle:
                ChaosAmount = 0.1f;
                TriggerRate = 2f;
                DurationMin = 30f;
                DurationMax = 80f;
                Mix = 0.3f;
                break;

            case GlitchPreset.Moderate:
                ChaosAmount = 0.3f;
                TriggerRate = 4f;
                DurationMin = 50f;
                DurationMax = 150f;
                Mix = 0.5f;
                break;

            case GlitchPreset.Extreme:
                ChaosAmount = 0.8f;
                TriggerRate = 8f;
                DurationMin = 100f;
                DurationMax = 400f;
                Mix = 0.9f;
                break;

            case GlitchPreset.Rhythmic:
                ChaosAmount = 0.2f;
                TriggerRate = 4f;
                DurationMin = 50f;
                DurationMax = 100f;
                Mix = 0.6f;
                SyncToTempo = true;
                // Set up rhythmic pattern
                for (int i = 0; i < PatternSteps.Count; i++)
                {
                    PatternSteps[i].IsActive = i % 2 == 0;
                    PatternSteps[i].SelectedEffect = GlitchEffectType.Gate;
                }
                break;

            case GlitchPreset.Chaotic:
                ChaosAmount = 1.0f;
                TriggerRate = 16f;
                DurationMin = 20f;
                DurationMax = 300f;
                Mix = 0.7f;
                // Enable all effects
                foreach (var module in EffectModules)
                {
                    module.IsEnabled = true;
                }
                break;

            case GlitchPreset.Retro:
                ChaosAmount = 0.4f;
                TriggerRate = 2f;
                DurationMin = 100f;
                DurationMax = 300f;
                Mix = 0.6f;
                // Focus on bit crush and sample rate reduction
                foreach (var module in EffectModules)
                {
                    module.IsEnabled = module.EffectType is GlitchEffectType.BitReduction
                        or GlitchEffectType.SampleRateReduction;
                }
                break;

            case GlitchPreset.Digital:
                ChaosAmount = 0.5f;
                TriggerRate = 8f;
                DurationMin = 30f;
                DurationMax = 100f;
                Mix = 0.7f;
                // Focus on buffer repeat and gate
                foreach (var module in EffectModules)
                {
                    module.IsEnabled = module.EffectType is GlitchEffectType.BufferRepeat
                        or GlitchEffectType.Gate
                        or GlitchEffectType.BitReduction;
                }
                break;

            case GlitchPreset.Tape:
                ChaosAmount = 0.3f;
                TriggerRate = 1f;
                DurationMin = 200f;
                DurationMax = 800f;
                Mix = 0.8f;
                // Focus on tape-related effects
                foreach (var module in EffectModules)
                {
                    module.IsEnabled = module.EffectType is GlitchEffectType.TapeStop
                        or GlitchEffectType.Stretch
                        or GlitchEffectType.Reverse;
                }
                break;
        }

        StatusMessage = $"Applied {preset} preset";
    }

    [RelayCommand]
    private void ResetAll()
    {
        ChaosAmount = 0.3f;
        TriggerRate = 4f;
        DurationMin = 50f;
        DurationMax = 200f;
        Mix = 0.5f;
        SyncToTempo = true;
        IsEnabled = true;
        IsBypassed = false;

        foreach (var module in EffectModules)
        {
            module.IsEnabled = true;
            module.Amount = 0.5f;
        }

        ClearPattern();
        StatusMessage = "Reset to defaults";
    }

    [RelayCommand]
    private void ToggleBypass()
    {
        IsBypassed = !IsBypassed;
        StatusMessage = IsBypassed ? "Effect bypassed" : "Effect active";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Sets the tempo from external source.
    /// </summary>
    public void SetTempo(float tempo)
    {
        Tempo = tempo;
    }

    /// <summary>
    /// Updates the input waveform data from audio engine.
    /// </summary>
    public void UpdateInputWaveform(float[] samples)
    {
        if (samples.Length != _inputWaveform.Length)
        {
            Array.Resize(ref _inputWaveform, samples.Length);
            Array.Resize(ref _outputWaveform, samples.Length);
        }
        Array.Copy(samples, _inputWaveform, samples.Length);
    }

    /// <summary>
    /// Gets the processed output waveform.
    /// </summary>
    public float[] GetOutputWaveform()
    {
        return _outputWaveform;
    }

    /// <summary>
    /// Enables or disables a specific effect module.
    /// </summary>
    public void SetEffectEnabled(GlitchEffectType effectType, bool enabled)
    {
        foreach (var module in EffectModules)
        {
            if (module.EffectType == effectType)
            {
                module.IsEnabled = enabled;
                break;
            }
        }
    }

    /// <summary>
    /// Sets a pattern step configuration.
    /// </summary>
    public void SetPatternStep(int stepIndex, bool active, GlitchEffectType? effect, float intensity = 1.0f)
    {
        if (stepIndex >= 0 && stepIndex < PatternSteps.Count)
        {
            var step = PatternSteps[stepIndex];
            step.IsActive = active;
            step.SelectedEffect = effect;
            step.Intensity = intensity;
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _glitchTimer?.Stop();
        _glitchTimer?.Dispose();
        _glitchTimer = null;

        _waveformTimer?.Stop();
        _waveformTimer?.Dispose();
        _waveformTimer = null;
    }

    #endregion
}

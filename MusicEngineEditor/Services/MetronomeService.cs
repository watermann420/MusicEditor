// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: Metronome/click track service.

using System;
using System.IO;
using MusicEngine.Core;

namespace MusicEngineEditor.Services;

/// <summary>
/// Sound type options for the metronome click.
/// </summary>
public enum MetronomeSoundType
{
    /// <summary>Standard sine wave click.</summary>
    Sine,

    /// <summary>Woodblock-style percussive sound.</summary>
    Wood,

    /// <summary>Stick/rimshot-style click.</summary>
    Stick,

    /// <summary>User-provided custom WAV file.</summary>
    Custom
}

/// <summary>
/// Service wrapper for the Metronome providing enable/disable toggle,
/// volume control, sound selection, and integration with the sequencer.
/// </summary>
public class MetronomeService : IDisposable
{
    private Metronome? _metronome;
    private Sequencer? _sequencer;
    private bool _disposed;

    /// <summary>
    /// Event raised when metronome settings change.
    /// </summary>
    public event EventHandler? SettingsChanged;

    /// <summary>
    /// Gets or sets whether the metronome is enabled.
    /// </summary>
    public bool IsEnabled
    {
        get => _metronome?.Enabled ?? false;
        set
        {
            if (_metronome != null)
            {
                _metronome.Enabled = value;
                OnSettingsChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the metronome volume (0.0 to 1.0).
    /// </summary>
    public float Volume
    {
        get => _metronome?.Volume ?? 0.7f;
        set
        {
            if (_metronome != null)
            {
                _metronome.Volume = Math.Clamp(value, 0f, 1f);
                OnSettingsChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets whether accent on the first beat is enabled.
    /// </summary>
    public bool AccentEnabled
    {
        get => _metronome?.AccentFirstBeat ?? true;
        set
        {
            if (_metronome != null)
            {
                _metronome.AccentFirstBeat = value;
                OnSettingsChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the accent volume multiplier (1.0 to 3.0).
    /// </summary>
    public float AccentVolume
    {
        get => _metronome?.AccentVolume ?? 1.5f;
        set
        {
            if (_metronome != null)
            {
                _metronome.AccentVolume = value;
                OnSettingsChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the sound type for the metronome.
    /// </summary>
    public MetronomeSoundType SoundType
    {
        get => _soundType;
        set
        {
            if (_soundType != value)
            {
                _soundType = value;
                ApplySoundType();
                OnSettingsChanged();
            }
        }
    }
    private MetronomeSoundType _soundType = MetronomeSoundType.Sine;

    /// <summary>
    /// Gets or sets the BPM (beats per minute).
    /// </summary>
    public double Bpm
    {
        get => _metronome?.Bpm ?? 120.0;
        set
        {
            if (_metronome != null)
            {
                _metronome.Bpm = value;
                OnSettingsChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the beats per bar (time signature numerator).
    /// </summary>
    public int BeatsPerBar
    {
        get => _metronome?.BeatsPerBar ?? 4;
        set
        {
            if (_metronome != null)
            {
                _metronome.BeatsPerBar = value;
                OnSettingsChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the count-in bars before playback (0, 1, 2, or 4).
    /// </summary>
    public int CountInBars { get; set; }

    /// <summary>
    /// Gets or sets the path to the custom click sound file.
    /// </summary>
    public string? CustomSoundPath
    {
        get => _customSoundPath;
        set
        {
            _customSoundPath = value;
            if (_soundType == MetronomeSoundType.Custom && !string.IsNullOrEmpty(value))
            {
                LoadCustomSound(value);
            }
        }
    }
    private string? _customSoundPath;

    /// <summary>
    /// Gets or sets the path to the custom accent click sound file.
    /// </summary>
    public string? CustomAccentSoundPath
    {
        get => _customAccentSoundPath;
        set
        {
            _customAccentSoundPath = value;
            if (_soundType == MetronomeSoundType.Custom && !string.IsNullOrEmpty(_customSoundPath))
            {
                LoadCustomSound(_customSoundPath, value);
            }
        }
    }
    private string? _customAccentSoundPath;

    /// <summary>
    /// Gets the current beat display (1-indexed).
    /// </summary>
    public int CurrentBeatDisplay => _metronome?.CurrentBeatDisplay ?? 1;

    /// <summary>
    /// Gets the underlying Metronome instance for audio integration.
    /// </summary>
    public Metronome? Metronome => _metronome;

    /// <summary>
    /// Initializes the metronome service with an optional sequencer for sync.
    /// </summary>
    /// <param name="sequencer">Optional sequencer for beat synchronization.</param>
    /// <param name="sampleRate">Optional sample rate (uses default if not specified).</param>
    public void Initialize(Sequencer? sequencer = null, int? sampleRate = null)
    {
        _sequencer = sequencer;
        _metronome = new Metronome(sampleRate, sequencer);
        ApplySoundType();
    }

    /// <summary>
    /// Connects the metronome to a sequencer for beat synchronization.
    /// </summary>
    /// <param name="sequencer">The sequencer to sync with.</param>
    public void ConnectToSequencer(Sequencer sequencer)
    {
        _sequencer = sequencer;
        if (_metronome != null)
        {
            _metronome.Sequencer = sequencer;
        }
    }

    /// <summary>
    /// Toggles the metronome enabled state.
    /// </summary>
    public void Toggle()
    {
        IsEnabled = !IsEnabled;
    }

    /// <summary>
    /// Resets the metronome to the beginning of a bar.
    /// </summary>
    public void Reset()
    {
        _metronome?.Reset();
    }

    /// <summary>
    /// Plays a preview click sound.
    /// </summary>
    public void PlayPreview()
    {
        // Reset to trigger a click at the start
        _metronome?.Reset();
    }

    /// <summary>
    /// Loads a custom click sound from a WAV file.
    /// </summary>
    /// <param name="path">Path to the WAV file.</param>
    /// <param name="accentPath">Optional path to a separate accent sound file.</param>
    public void LoadCustomSound(string path, string? accentPath = null)
    {
        if (_metronome == null || string.IsNullOrWhiteSpace(path))
            return;

        try
        {
            if (File.Exists(path))
            {
                _metronome.LoadCustomSound(path, accentPath);
                _soundType = MetronomeSoundType.Custom;
                _customSoundPath = path;
                _customAccentSoundPath = accentPath;
                OnSettingsChanged();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load custom metronome sound: {ex.Message}");
        }
    }

    /// <summary>
    /// Sets the click frequency for generated sounds.
    /// </summary>
    /// <param name="normalFrequency">Frequency for normal beats (Hz).</param>
    /// <param name="accentFrequency">Frequency for accented beats (Hz).</param>
    public void SetClickFrequencies(double normalFrequency, double accentFrequency)
    {
        if (_metronome != null)
        {
            _metronome.ClickFrequency = normalFrequency;
            _metronome.AccentFrequency = accentFrequency;
            OnSettingsChanged();
        }
    }

    /// <summary>
    /// Sets the click duration in milliseconds.
    /// </summary>
    /// <param name="durationMs">Duration in milliseconds (5-100).</param>
    public void SetClickDuration(double durationMs)
    {
        if (_metronome != null)
        {
            _metronome.ClickDurationMs = durationMs;
            OnSettingsChanged();
        }
    }

    /// <summary>
    /// Applies the current sound type to the metronome.
    /// </summary>
    private void ApplySoundType()
    {
        if (_metronome == null) return;

        switch (_soundType)
        {
            case MetronomeSoundType.Sine:
                _metronome.ClickSound = ClickSound.Click;
                _metronome.ClickFrequency = 1000.0;
                _metronome.AccentFrequency = 1500.0;
                _metronome.ClickDurationMs = 15.0;
                break;

            case MetronomeSoundType.Wood:
                _metronome.ClickSound = ClickSound.Woodblock;
                _metronome.ClickFrequency = 800.0;
                _metronome.AccentFrequency = 1200.0;
                _metronome.ClickDurationMs = 20.0;
                break;

            case MetronomeSoundType.Stick:
                _metronome.ClickSound = ClickSound.Beep;
                _metronome.ClickFrequency = 1200.0;
                _metronome.AccentFrequency = 1800.0;
                _metronome.ClickDurationMs = 12.0;
                break;

            case MetronomeSoundType.Custom:
                if (!string.IsNullOrEmpty(_customSoundPath) && File.Exists(_customSoundPath))
                {
                    _metronome.LoadCustomSound(_customSoundPath, _customAccentSoundPath);
                }
                break;
        }
    }

    /// <summary>
    /// Raises the SettingsChanged event.
    /// </summary>
    protected virtual void OnSettingsChanged()
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Disposes the metronome service.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _metronome?.Dispose();
        _metronome = null;

        GC.SuppressFinalize(this);
    }
}

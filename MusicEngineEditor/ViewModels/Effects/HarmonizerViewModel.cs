// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Harmonizer effect control.

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Effects;

/// <summary>
/// Represents a harmony voice configuration in the ViewModel.
/// </summary>
public partial class HarmonyVoiceViewModel : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private int _interval;

    [ObservableProperty]
    private int _detune;

    [ObservableProperty]
    private double _level = 0.8;

    [ObservableProperty]
    private double _pan;

    [ObservableProperty]
    private double _delay;

    [ObservableProperty]
    private bool _isEnabled = true;

    [ObservableProperty]
    private bool _isMuted;

    /// <summary>
    /// Gets the formatted interval display string.
    /// </summary>
    public string IntervalDisplay => Interval == 0 ? "Unison" : $"{(Interval > 0 ? "+" : "")}{Interval} st";

    /// <summary>
    /// Gets the formatted detune display string.
    /// </summary>
    public string DetuneDisplay => $"{Detune:+0;-0;0} ct";

    /// <summary>
    /// Gets the formatted level display string.
    /// </summary>
    public string LevelDisplay => $"{Level * 100:0}%";

    /// <summary>
    /// Gets the formatted pan display string.
    /// </summary>
    public string PanDisplay
    {
        get
        {
            if (Math.Abs(Pan) < 0.01) return "C";
            if (Pan < 0) return $"L{(int)(-Pan * 100)}";
            return $"R{(int)(Pan * 100)}";
        }
    }

    /// <summary>
    /// Gets the formatted delay display string.
    /// </summary>
    public string DelayDisplay => $"{Delay:0} ms";

    partial void OnIntervalChanged(int value) => OnPropertyChanged(nameof(IntervalDisplay));
    partial void OnDetuneChanged(int value) => OnPropertyChanged(nameof(DetuneDisplay));
    partial void OnLevelChanged(double value) => OnPropertyChanged(nameof(LevelDisplay));
    partial void OnPanChanged(double value) => OnPropertyChanged(nameof(PanDisplay));
    partial void OnDelayChanged(double value) => OnPropertyChanged(nameof(DelayDisplay));
}

/// <summary>
/// Available musical scales for scale lock.
/// </summary>
public enum MusicalScale
{
    Major,
    Minor,
    HarmonicMinor,
    MelodicMinor,
    Dorian,
    Phrygian,
    Lydian,
    Mixolydian,
    Chromatic
}

/// <summary>
/// ViewModel for the Harmonizer effect control.
/// </summary>
public partial class HarmonizerViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private const int MaxVoices = 4;

    #region Observable Properties - Voices

    /// <summary>
    /// Gets the collection of harmony voices.
    /// </summary>
    public ObservableCollection<HarmonyVoiceViewModel> Voices { get; } = new();

    #endregion

    #region Observable Properties - Global Settings

    [ObservableProperty]
    private bool _isBypassed;

    [ObservableProperty]
    private bool _scaleLockEnabled;

    [ObservableProperty]
    private int _rootKey;

    [ObservableProperty]
    private MusicalScale _selectedScale = MusicalScale.Major;

    [ObservableProperty]
    private bool _formantCorrectionEnabled;

    [ObservableProperty]
    private bool _midiInputEnabled;

    [ObservableProperty]
    private double _mix = 0.5;

    [ObservableProperty]
    private int _inputNote = -1;

    #endregion

    #region Observable Properties - UI State

    [ObservableProperty]
    private bool _canAddVoice = true;

    [ObservableProperty]
    private string _currentInputNoteDisplay = "-";

    #endregion

    /// <summary>
    /// Gets the available root keys.
    /// </summary>
    public string[] RootKeys { get; } = { "C", "C#/Db", "D", "D#/Eb", "E", "F", "F#/Gb", "G", "G#/Ab", "A", "A#/Bb", "B" };

    /// <summary>
    /// Gets the available scales.
    /// </summary>
    public MusicalScale[] AvailableScales { get; } = (MusicalScale[])Enum.GetValues(typeof(MusicalScale));

    /// <summary>
    /// Event raised when a voice parameter changes.
    /// </summary>
    public event EventHandler<VoiceParameterChangedEventArgs>? VoiceParameterChanged;

    /// <summary>
    /// Event raised when global settings change.
    /// </summary>
    public event EventHandler<GlobalSettingChangedEventArgs>? GlobalSettingChanged;

    public HarmonizerViewModel()
    {
        // Add one default voice
        AddVoiceInternal(5); // Perfect 4th above
    }

    #region Property Change Handlers

    partial void OnIsBypassedChanged(bool value)
    {
        GlobalSettingChanged?.Invoke(this, new GlobalSettingChangedEventArgs("Bypass", value ? 1.0 : 0.0));
        StatusMessage = value ? "Harmonizer bypassed" : "Harmonizer active";
    }

    partial void OnScaleLockEnabledChanged(bool value)
    {
        GlobalSettingChanged?.Invoke(this, new GlobalSettingChangedEventArgs("ScaleLock", value ? 1.0 : 0.0));
        StatusMessage = value ? "Scale lock enabled" : "Scale lock disabled";
    }

    partial void OnRootKeyChanged(int value)
    {
        GlobalSettingChanged?.Invoke(this, new GlobalSettingChangedEventArgs("RootKey", value));
    }

    partial void OnSelectedScaleChanged(MusicalScale value)
    {
        GlobalSettingChanged?.Invoke(this, new GlobalSettingChangedEventArgs("Scale", (int)value));
    }

    partial void OnFormantCorrectionEnabledChanged(bool value)
    {
        GlobalSettingChanged?.Invoke(this, new GlobalSettingChangedEventArgs("FormantCorrection", value ? 1.0 : 0.0));
        StatusMessage = value ? "Formant correction enabled" : "Formant correction disabled";
    }

    partial void OnMidiInputEnabledChanged(bool value)
    {
        GlobalSettingChanged?.Invoke(this, new GlobalSettingChangedEventArgs("MidiInput", value ? 1.0 : 0.0));
        StatusMessage = value ? "MIDI input enabled" : "MIDI input disabled";
    }

    partial void OnMixChanged(double value)
    {
        GlobalSettingChanged?.Invoke(this, new GlobalSettingChangedEventArgs("Mix", value));
    }

    partial void OnInputNoteChanged(int value)
    {
        CurrentInputNoteDisplay = value >= 0 ? GetNoteName(value) : "-";
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void AddVoice()
    {
        if (Voices.Count >= MaxVoices)
        {
            StatusMessage = "Maximum 4 voices reached";
            return;
        }

        AddVoiceInternal(0);
        StatusMessage = $"Voice {Voices.Count} added";
    }

    [RelayCommand]
    private void RemoveVoice(HarmonyVoiceViewModel voice)
    {
        if (voice == null) return;

        Voices.Remove(voice);

        // Reindex remaining voices
        for (int i = 0; i < Voices.Count; i++)
        {
            Voices[i].Index = i;
        }

        UpdateCanAddVoice();
        StatusMessage = "Voice removed";
    }

    [RelayCommand]
    private void ToggleVoiceEnable(HarmonyVoiceViewModel voice)
    {
        if (voice == null) return;
        voice.IsEnabled = !voice.IsEnabled;
        VoiceParameterChanged?.Invoke(this, new VoiceParameterChangedEventArgs(voice.Index, "Enable", voice.IsEnabled ? 1.0 : 0.0));
    }

    [RelayCommand]
    private void ToggleVoiceMute(HarmonyVoiceViewModel voice)
    {
        if (voice == null) return;
        voice.IsMuted = !voice.IsMuted;
        VoiceParameterChanged?.Invoke(this, new VoiceParameterChangedEventArgs(voice.Index, "Mute", voice.IsMuted ? 1.0 : 0.0));
    }

    [RelayCommand]
    private void Reset()
    {
        Voices.Clear();

        IsBypassed = false;
        ScaleLockEnabled = false;
        RootKey = 0;
        SelectedScale = MusicalScale.Major;
        FormantCorrectionEnabled = false;
        MidiInputEnabled = false;
        Mix = 0.5;
        InputNote = -1;

        AddVoiceInternal(5); // Perfect 4th

        StatusMessage = "Reset to defaults";
    }

    [RelayCommand]
    private void LoadPreset(string presetName)
    {
        switch (presetName)
        {
            case "Thirds":
                LoadThirdsPreset();
                break;
            case "Fifths":
                LoadFifthsPreset();
                break;
            case "Octaves":
                LoadOctavesPreset();
                break;
            case "Barbershop":
                LoadBarbershopPreset();
                break;
            case "Choir":
                LoadChoirPreset();
                break;
            default:
                StatusMessage = "Unknown preset";
                return;
        }

        StatusMessage = $"Loaded preset: {presetName}";
    }

    #endregion

    #region Voice Management

    private void AddVoiceInternal(int interval)
    {
        var voice = new HarmonyVoiceViewModel
        {
            Index = Voices.Count,
            Interval = interval,
            Detune = 0,
            Level = 0.8,
            Pan = 0,
            Delay = 0,
            IsEnabled = true,
            IsMuted = false
        };

        // Subscribe to property changes
        voice.PropertyChanged += (s, e) =>
        {
            if (s is HarmonyVoiceViewModel v)
            {
                switch (e.PropertyName)
                {
                    case nameof(HarmonyVoiceViewModel.Interval):
                        VoiceParameterChanged?.Invoke(this, new VoiceParameterChangedEventArgs(v.Index, "Interval", v.Interval));
                        break;
                    case nameof(HarmonyVoiceViewModel.Detune):
                        VoiceParameterChanged?.Invoke(this, new VoiceParameterChangedEventArgs(v.Index, "Detune", v.Detune));
                        break;
                    case nameof(HarmonyVoiceViewModel.Level):
                        VoiceParameterChanged?.Invoke(this, new VoiceParameterChangedEventArgs(v.Index, "Level", v.Level));
                        break;
                    case nameof(HarmonyVoiceViewModel.Pan):
                        VoiceParameterChanged?.Invoke(this, new VoiceParameterChangedEventArgs(v.Index, "Pan", v.Pan));
                        break;
                    case nameof(HarmonyVoiceViewModel.Delay):
                        VoiceParameterChanged?.Invoke(this, new VoiceParameterChangedEventArgs(v.Index, "Delay", v.Delay));
                        break;
                }
            }
        };

        Voices.Add(voice);
        UpdateCanAddVoice();
    }

    private void UpdateCanAddVoice()
    {
        CanAddVoice = Voices.Count < MaxVoices;
    }

    #endregion

    #region Preset Loading

    private void LoadThirdsPreset()
    {
        Voices.Clear();
        AddVoiceInternal(4);  // Major 3rd up
        AddVoiceInternal(-3); // Minor 3rd down
        Mix = 0.6;
        ScaleLockEnabled = true;
        SelectedScale = MusicalScale.Major;
    }

    private void LoadFifthsPreset()
    {
        Voices.Clear();
        AddVoiceInternal(7);  // Perfect 5th up
        AddVoiceInternal(-5); // Perfect 4th down (inverted 5th)
        Mix = 0.5;
    }

    private void LoadOctavesPreset()
    {
        Voices.Clear();
        AddVoiceInternal(12);  // Octave up
        AddVoiceInternal(-12); // Octave down
        Mix = 0.4;
    }

    private void LoadBarbershopPreset()
    {
        Voices.Clear();
        AddVoiceInternal(4);   // Major 3rd
        AddVoiceInternal(7);   // Perfect 5th
        AddVoiceInternal(-5);  // Perfect 4th down
        Mix = 0.7;
        ScaleLockEnabled = true;
        SelectedScale = MusicalScale.Major;
    }

    private void LoadChoirPreset()
    {
        Voices.Clear();
        AddVoiceInternal(12);  // Octave up
        AddVoiceInternal(7);   // Perfect 5th
        AddVoiceInternal(-12); // Octave down
        AddVoiceInternal(-5);  // Perfect 4th down

        // Spread voices in stereo
        Voices[0].Pan = -0.5;
        Voices[0].Delay = 10;
        Voices[1].Pan = 0.5;
        Voices[1].Delay = 15;
        Voices[2].Pan = -0.3;
        Voices[2].Delay = 5;
        Voices[3].Pan = 0.3;
        Voices[3].Delay = 8;

        Mix = 0.6;
        FormantCorrectionEnabled = true;
    }

    #endregion

    #region Harmony Calculation

    /// <summary>
    /// Calculates the harmony notes for a given input note.
    /// </summary>
    public int[] GetHarmonyNotes(int inputNote)
    {
        var notes = new System.Collections.Generic.List<int>();

        foreach (var voice in Voices)
        {
            if (voice.IsEnabled && !voice.IsMuted)
            {
                int harmonyNote = inputNote + voice.Interval;

                if (ScaleLockEnabled)
                {
                    harmonyNote = SnapToScale(harmonyNote, RootKey, SelectedScale);
                }

                if (harmonyNote >= 0 && harmonyNote <= 127)
                {
                    notes.Add(harmonyNote);
                }
            }
        }

        return notes.ToArray();
    }

    private int SnapToScale(int note, int key, MusicalScale scale)
    {
        int[] scaleIntervals = GetScaleIntervals(scale);
        if (scaleIntervals.Length == 0) return note;

        int noteInOctave = ((note - key) % 12 + 12) % 12;
        int octave = (note - key) / 12;

        // Find closest scale degree
        int closestInterval = 0;
        int minDistance = 12;

        foreach (int interval in scaleIntervals)
        {
            int distance = Math.Abs(noteInOctave - interval);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestInterval = interval;
            }
        }

        return key + octave * 12 + closestInterval;
    }

    private static int[] GetScaleIntervals(MusicalScale scale)
    {
        return scale switch
        {
            MusicalScale.Major => new[] { 0, 2, 4, 5, 7, 9, 11 },
            MusicalScale.Minor => new[] { 0, 2, 3, 5, 7, 8, 10 },
            MusicalScale.HarmonicMinor => new[] { 0, 2, 3, 5, 7, 8, 11 },
            MusicalScale.MelodicMinor => new[] { 0, 2, 3, 5, 7, 9, 11 },
            MusicalScale.Dorian => new[] { 0, 2, 3, 5, 7, 9, 10 },
            MusicalScale.Phrygian => new[] { 0, 1, 3, 5, 7, 8, 10 },
            MusicalScale.Lydian => new[] { 0, 2, 4, 6, 7, 9, 11 },
            MusicalScale.Mixolydian => new[] { 0, 2, 4, 5, 7, 9, 10 },
            MusicalScale.Chromatic => new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 },
            _ => new[] { 0, 2, 4, 5, 7, 9, 11 }
        };
    }

    #endregion

    #region Helper Methods

    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    private static string GetNoteName(int midiNote)
    {
        if (midiNote < 0 || midiNote > 127) return "-";
        int noteIndex = midiNote % 12;
        int octave = (midiNote / 12) - 1;
        return $"{NoteNames[noteIndex]}{octave}";
    }

    #endregion

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Voices.Clear();
    }
}

#region Event Arguments

/// <summary>
/// Event arguments for voice parameter changes.
/// </summary>
public class VoiceParameterChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the voice index.
    /// </summary>
    public int VoiceIndex { get; }

    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    public VoiceParameterChangedEventArgs(int voiceIndex, string parameterName, double value)
    {
        VoiceIndex = voiceIndex;
        ParameterName = parameterName;
        Value = value;
    }
}

/// <summary>
/// Event arguments for global setting changes.
/// </summary>
public class GlobalSettingChangedEventArgs : EventArgs
{
    /// <summary>
    /// Gets the setting name.
    /// </summary>
    public string SettingName { get; }

    /// <summary>
    /// Gets the new value.
    /// </summary>
    public double Value { get; }

    public GlobalSettingChangedEventArgs(string settingName, double value)
    {
        SettingName = settingName;
        Value = value;
    }
}

#endregion

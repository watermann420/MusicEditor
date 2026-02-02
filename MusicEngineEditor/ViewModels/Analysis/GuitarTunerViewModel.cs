// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for Guitar Tuner panel with pitch detection and tuning presets.

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Analysis;

/// <summary>
/// ViewModel for the Guitar Tuner panel.
/// Provides pitch detection, tuning presets, and visual feedback for guitar tuning.
/// </summary>
public partial class GuitarTunerViewModel : ViewModelBase
{
    #region Constants

    /// <summary>
    /// Standard note names for display.
    /// </summary>
    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

    #endregion

    #region Observable Properties

    /// <summary>
    /// Detected frequency in Hz.
    /// </summary>
    [ObservableProperty]
    private double _detectedFrequency;

    /// <summary>
    /// Deviation from target note in cents (-50 to +50).
    /// </summary>
    [ObservableProperty]
    private double _centsDeviation;

    /// <summary>
    /// Current detected note name (e.g., "A", "C#", "Eb").
    /// </summary>
    [ObservableProperty]
    private string _noteName = "--";

    /// <summary>
    /// Current detected octave number.
    /// </summary>
    [ObservableProperty]
    private int _octave = 4;

    /// <summary>
    /// Input signal level (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private double _inputLevel;

    /// <summary>
    /// Whether the note is in tune (within tolerance).
    /// </summary>
    [ObservableProperty]
    private bool _isInTune;

    /// <summary>
    /// Whether auto-detect mode is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isAutoDetect = true;

    /// <summary>
    /// Whether strobe tuner mode is enabled.
    /// </summary>
    [ObservableProperty]
    private bool _isStrobeMode;

    /// <summary>
    /// Current strobe phase for animation (0.0 to 1.0).
    /// </summary>
    [ObservableProperty]
    private double _strobePhase;

    /// <summary>
    /// Reference pitch for A4 in Hz (default 440).
    /// </summary>
    [ObservableProperty]
    private double _referencePitch = 440.0;

    /// <summary>
    /// Tolerance in cents for "in tune" indication.
    /// </summary>
    [ObservableProperty]
    private double _tuneTolerance = 5.0;

    /// <summary>
    /// Currently selected tuning preset.
    /// </summary>
    [ObservableProperty]
    private TuningPreset? _selectedTuningPreset;

    /// <summary>
    /// Currently selected string index (0-5 for 6-string guitar).
    /// </summary>
    [ObservableProperty]
    private int _selectedStringIndex = -1;

    /// <summary>
    /// Target frequency for the selected string.
    /// </summary>
    [ObservableProperty]
    private double _targetFrequency;

    /// <summary>
    /// Target note name for the selected string.
    /// </summary>
    [ObservableProperty]
    private string _targetNoteName = "--";

    /// <summary>
    /// Whether the tuner is actively listening.
    /// </summary>
    [ObservableProperty]
    private bool _isListening;

    /// <summary>
    /// Whether there is sufficient signal to detect pitch.
    /// </summary>
    [ObservableProperty]
    private bool _hasSignal;

    /// <summary>
    /// Display text for frequency (formatted).
    /// </summary>
    [ObservableProperty]
    private string _frequencyDisplay = "--- Hz";

    /// <summary>
    /// Display text for cents deviation (formatted).
    /// </summary>
    [ObservableProperty]
    private string _centsDisplay = "0";

    #endregion

    #region Collections

    /// <summary>
    /// Available tuning presets.
    /// </summary>
    public ObservableCollection<TuningPreset> TuningPresets { get; } = new();

    /// <summary>
    /// Strings for the current tuning preset.
    /// </summary>
    public ObservableCollection<GuitarString> GuitarStrings { get; } = new();

    #endregion

    #region Constructor

    public GuitarTunerViewModel()
    {
        InitializeTuningPresets();
        SelectedTuningPreset = TuningPresets[0]; // Standard tuning
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void StartListening()
    {
        IsListening = true;
        StatusMessage = "Listening...";
    }

    [RelayCommand]
    private void StopListening()
    {
        IsListening = false;
        StatusMessage = "Stopped";
    }

    [RelayCommand]
    private void ToggleListening()
    {
        if (IsListening)
        {
            StopListening();
        }
        else
        {
            StartListening();
        }
    }

    [RelayCommand]
    private void ToggleAutoDetect()
    {
        IsAutoDetect = !IsAutoDetect;
        if (IsAutoDetect)
        {
            SelectedStringIndex = -1;
        }
    }

    [RelayCommand]
    private void ToggleStrobeMode()
    {
        IsStrobeMode = !IsStrobeMode;
    }

    [RelayCommand]
    private void SelectString(int stringIndex)
    {
        if (stringIndex >= 0 && stringIndex < GuitarStrings.Count)
        {
            SelectedStringIndex = stringIndex;
            IsAutoDetect = false;
            UpdateTargetFromSelectedString();
        }
    }

    [RelayCommand]
    private void IncrementReferencePitch()
    {
        if (ReferencePitch < 446)
        {
            ReferencePitch += 1;
            RecalculateStringFrequencies();
        }
    }

    [RelayCommand]
    private void DecrementReferencePitch()
    {
        if (ReferencePitch > 432)
        {
            ReferencePitch -= 1;
            RecalculateStringFrequencies();
        }
    }

    [RelayCommand]
    private void ResetReferencePitch()
    {
        ReferencePitch = 440.0;
        RecalculateStringFrequencies();
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the tuner with new pitch detection data.
    /// </summary>
    /// <param name="frequency">Detected frequency in Hz.</param>
    /// <param name="level">Input signal level (0.0 to 1.0).</param>
    public void UpdatePitchData(double frequency, double level)
    {
        InputLevel = level;
        HasSignal = level > 0.01;

        if (!HasSignal || frequency <= 0)
        {
            DetectedFrequency = 0;
            NoteName = "--";
            CentsDeviation = 0;
            IsInTune = false;
            FrequencyDisplay = "--- Hz";
            CentsDisplay = "0";
            return;
        }

        DetectedFrequency = frequency;
        FrequencyDisplay = $"{frequency:F1} Hz";

        // Calculate note and cents deviation
        var (noteName, octave, cents) = FrequencyToNote(frequency);
        NoteName = noteName;
        Octave = octave;
        CentsDeviation = cents;
        CentsDisplay = cents >= 0 ? $"+{cents:F0}" : $"{cents:F0}";

        // Check if in tune
        double targetCents = 0;
        if (!IsAutoDetect && SelectedStringIndex >= 0 && SelectedStringIndex < GuitarStrings.Count)
        {
            // Calculate cents from target frequency
            double targetFreq = GuitarStrings[SelectedStringIndex].Frequency;
            targetCents = 1200 * Math.Log2(frequency / targetFreq);
            CentsDeviation = targetCents;
            CentsDisplay = targetCents >= 0 ? $"+{targetCents:F0}" : $"{targetCents:F0}";
        }

        IsInTune = Math.Abs(CentsDeviation) <= TuneTolerance;

        // Update strobe phase if in strobe mode
        if (IsStrobeMode)
        {
            // Strobe speed based on cents deviation
            double strobeSpeed = CentsDeviation / 50.0; // Normalized to -1..+1
            StrobePhase = (StrobePhase + strobeSpeed * 0.1) % 1.0;
            if (StrobePhase < 0) StrobePhase += 1.0;
        }

        // Auto-detect string if enabled
        if (IsAutoDetect)
        {
            AutoDetectString(frequency);
        }
    }

    #endregion

    #region Private Methods

    private void InitializeTuningPresets()
    {
        TuningPresets.Add(new TuningPreset("Standard", "E A D G B E", new[] { "E2", "A2", "D3", "G3", "B3", "E4" }));
        TuningPresets.Add(new TuningPreset("Drop D", "D A D G B E", new[] { "D2", "A2", "D3", "G3", "B3", "E4" }));
        TuningPresets.Add(new TuningPreset("DADGAD", "D A D G A D", new[] { "D2", "A2", "D3", "G3", "A3", "D4" }));
        TuningPresets.Add(new TuningPreset("Open G", "D G D G B D", new[] { "D2", "G2", "D3", "G3", "B3", "D4" }));
        TuningPresets.Add(new TuningPreset("Open D", "D A D F# A D", new[] { "D2", "A2", "D3", "F#3", "A3", "D4" }));
        TuningPresets.Add(new TuningPreset("Open E", "E B E G# B E", new[] { "E2", "B2", "E3", "G#3", "B3", "E4" }));
        TuningPresets.Add(new TuningPreset("Half Step Down", "Eb Ab Db Gb Bb Eb", new[] { "D#2", "G#2", "C#3", "F#3", "A#3", "D#4" }));
        TuningPresets.Add(new TuningPreset("Full Step Down", "D G C F A D", new[] { "D2", "G2", "C3", "F3", "A3", "D4" }));
        TuningPresets.Add(new TuningPreset("Drop C", "C G C F A D", new[] { "C2", "G2", "C3", "F3", "A3", "D4" }));
        TuningPresets.Add(new TuningPreset("Open C", "C G C G C E", new[] { "C2", "G2", "C3", "G3", "C4", "E4" }));
    }

    partial void OnSelectedTuningPresetChanged(TuningPreset? value)
    {
        if (value != null)
        {
            UpdateGuitarStrings(value);
        }
    }

    private void UpdateGuitarStrings(TuningPreset preset)
    {
        GuitarStrings.Clear();

        for (int i = 0; i < preset.Notes.Length; i++)
        {
            var noteStr = preset.Notes[i];
            var freq = NoteToFrequency(noteStr);
            GuitarStrings.Add(new GuitarString
            {
                Index = i,
                NoteName = noteStr,
                Frequency = freq,
                StringNumber = 6 - i // String 6 is lowest (E2), String 1 is highest (E4)
            });
        }

        if (SelectedStringIndex >= 0)
        {
            UpdateTargetFromSelectedString();
        }
    }

    private void RecalculateStringFrequencies()
    {
        foreach (var guitarString in GuitarStrings)
        {
            guitarString.Frequency = NoteToFrequency(guitarString.NoteName);
        }

        if (SelectedStringIndex >= 0)
        {
            UpdateTargetFromSelectedString();
        }
    }

    private void UpdateTargetFromSelectedString()
    {
        if (SelectedStringIndex >= 0 && SelectedStringIndex < GuitarStrings.Count)
        {
            var selectedString = GuitarStrings[SelectedStringIndex];
            TargetFrequency = selectedString.Frequency;
            TargetNoteName = selectedString.NoteName;
        }
        else
        {
            TargetFrequency = 0;
            TargetNoteName = "--";
        }
    }

    private void AutoDetectString(double frequency)
    {
        if (GuitarStrings.Count == 0) return;

        // Find the closest string to the detected frequency
        int closestIndex = 0;
        double minCentsDiff = double.MaxValue;

        for (int i = 0; i < GuitarStrings.Count; i++)
        {
            double stringFreq = GuitarStrings[i].Frequency;
            double centsDiff = Math.Abs(1200 * Math.Log2(frequency / stringFreq));

            if (centsDiff < minCentsDiff)
            {
                minCentsDiff = centsDiff;
                closestIndex = i;
            }
        }

        // Only update if within 100 cents (one semitone)
        if (minCentsDiff <= 100)
        {
            SelectedStringIndex = closestIndex;
            UpdateTargetFromSelectedString();
        }
    }

    private (string noteName, int octave, double cents) FrequencyToNote(double frequency)
    {
        // Calculate semitones from A4
        double semitonesFromA4 = 12 * Math.Log2(frequency / ReferencePitch);
        int roundedSemitones = (int)Math.Round(semitonesFromA4);
        double cents = (semitonesFromA4 - roundedSemitones) * 100;

        // A4 is MIDI note 69 (9th note in octave 4)
        int midiNote = 69 + roundedSemitones;
        int noteIndex = ((midiNote % 12) + 12) % 12;
        int octave = (midiNote / 12) - 1;

        return (NoteNames[noteIndex], octave, cents);
    }

    private double NoteToFrequency(string noteString)
    {
        // Parse note string like "A4", "C#3", "Eb2", "D#4"
        if (string.IsNullOrEmpty(noteString) || noteString.Length < 2)
            return 440.0;

        string notePart;
        int octave;

        // Handle notes with accidentals (2 characters for note)
        if (noteString.Length >= 2 && (noteString[1] == '#' || noteString[1] == 'b'))
        {
            notePart = noteString.Substring(0, 2);
            if (!int.TryParse(noteString.Substring(2), out octave))
                octave = 4;
        }
        else
        {
            notePart = noteString.Substring(0, 1);
            if (!int.TryParse(noteString.Substring(1), out octave))
                octave = 4;
        }

        // Find note index
        int noteIndex = GetNoteIndex(notePart);

        // Calculate MIDI note number
        int midiNote = (octave + 1) * 12 + noteIndex;

        // Calculate frequency (A4 = MIDI 69)
        double semitones = midiNote - 69;
        return ReferencePitch * Math.Pow(2, semitones / 12.0);
    }

    private static int GetNoteIndex(string note)
    {
        return note.ToUpper() switch
        {
            "C" => 0,
            "C#" or "DB" => 1,
            "D" => 2,
            "D#" or "EB" => 3,
            "E" => 4,
            "F" => 5,
            "F#" or "GB" => 6,
            "G" => 7,
            "G#" or "AB" => 8,
            "A" => 9,
            "A#" or "BB" => 10,
            "B" => 11,
            _ => 9 // Default to A
        };
    }

    #endregion
}

#region Supporting Types

/// <summary>
/// Represents a guitar tuning preset.
/// </summary>
public class TuningPreset
{
    public string Name { get; }
    public string Description { get; }
    public string[] Notes { get; }

    public TuningPreset(string name, string description, string[] notes)
    {
        Name = name;
        Description = description;
        Notes = notes;
    }

    public override string ToString() => Name;
}

/// <summary>
/// Represents a guitar string with its tuning information.
/// </summary>
public partial class GuitarString : ObservableObject
{
    [ObservableProperty]
    private int _index;

    [ObservableProperty]
    private string _noteName = string.Empty;

    [ObservableProperty]
    private double _frequency;

    [ObservableProperty]
    private int _stringNumber;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isInTune;

    /// <summary>
    /// Gets the formatted frequency string.
    /// </summary>
    public string FrequencyDisplay => $"{Frequency:F1} Hz";
}

#endregion

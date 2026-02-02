// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Auto-Tune pitch correction effect.

using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Effects;

/// <summary>
/// Represents a musical scale with its interval pattern.
/// </summary>
public class ScaleDefinition
{
    /// <summary>
    /// Gets the scale name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the scale intervals (semitones from root).
    /// True = note is in scale.
    /// </summary>
    public bool[] Notes { get; }

    /// <summary>
    /// Creates a new scale definition.
    /// </summary>
    public ScaleDefinition(string name, bool[] notes)
    {
        Name = name;
        Notes = notes;
    }
}

/// <summary>
/// ViewModel for the Auto-Tune pitch correction effect editor.
/// </summary>
public partial class AutoTuneViewModel : ViewModelBase, IDisposable
{
    #region Private Fields

    private bool _disposed;
    private readonly bool[] _bypassedNotes = new bool[12];

    #endregion

    #region Observable Properties

    [ObservableProperty]
    private int _rootNote;

    [ObservableProperty]
    private int _scaleType;

    [ObservableProperty]
    private double _correctionSpeed = 100.0;

    [ObservableProperty]
    private double _humanizeAmount;

    [ObservableProperty]
    private double _retuneSpeed = 50.0;

    [ObservableProperty]
    private bool _formantPreservation = true;

    [ObservableProperty]
    private bool _isBypassed;

    [ObservableProperty]
    private double _inputPitch;

    [ObservableProperty]
    private double _outputPitch;

    [ObservableProperty]
    private string _inputPitchDisplay = "--";

    [ObservableProperty]
    private string _outputPitchDisplay = "--";

    [ObservableProperty]
    private int _inputPitchCents;

    [ObservableProperty]
    private int _outputPitchCents;

    [ObservableProperty]
    private string _presetName = "Default";

    #endregion

    #region Collections

    /// <summary>
    /// Gets the available root notes.
    /// </summary>
    public ObservableCollection<string> RootNotes { get; } = new()
    {
        "C", "C#/Db", "D", "D#/Eb", "E", "F", "F#/Gb", "G", "G#/Ab", "A", "A#/Bb", "B"
    };

    /// <summary>
    /// Gets the available scale types.
    /// </summary>
    public ObservableCollection<ScaleDefinition> ScaleTypes { get; } = new()
    {
        // Chromatic - all notes
        new ScaleDefinition("Chromatic", new[] { true, true, true, true, true, true, true, true, true, true, true, true }),

        // Major scale: W-W-H-W-W-W-H
        new ScaleDefinition("Major", new[] { true, false, true, false, true, true, false, true, false, true, false, true }),

        // Natural Minor: W-H-W-W-H-W-W
        new ScaleDefinition("Minor (Natural)", new[] { true, false, true, true, false, true, false, true, true, false, true, false }),

        // Harmonic Minor: W-H-W-W-H-A-H (A = augmented second)
        new ScaleDefinition("Minor (Harmonic)", new[] { true, false, true, true, false, true, false, true, true, false, false, true }),

        // Melodic Minor (ascending): W-H-W-W-W-W-H
        new ScaleDefinition("Minor (Melodic)", new[] { true, false, true, true, false, true, false, true, false, true, false, true }),

        // Dorian: W-H-W-W-W-H-W
        new ScaleDefinition("Dorian", new[] { true, false, true, true, false, true, false, true, false, true, true, false }),

        // Phrygian: H-W-W-W-H-W-W
        new ScaleDefinition("Phrygian", new[] { true, true, false, true, false, true, false, true, true, false, true, false }),

        // Lydian: W-W-W-H-W-W-H
        new ScaleDefinition("Lydian", new[] { true, false, true, false, true, false, true, true, false, true, false, true }),

        // Mixolydian: W-W-H-W-W-H-W
        new ScaleDefinition("Mixolydian", new[] { true, false, true, false, true, true, false, true, false, true, true, false }),

        // Locrian: H-W-W-H-W-W-W
        new ScaleDefinition("Locrian", new[] { true, true, false, true, false, true, true, false, true, false, true, false }),

        // Pentatonic Major: 1-2-3-5-6
        new ScaleDefinition("Pentatonic Major", new[] { true, false, true, false, true, false, false, true, false, true, false, false }),

        // Pentatonic Minor: 1-b3-4-5-b7
        new ScaleDefinition("Pentatonic Minor", new[] { true, false, false, true, false, true, false, true, false, false, true, false }),

        // Blues: 1-b3-4-b5-5-b7
        new ScaleDefinition("Blues", new[] { true, false, false, true, false, true, true, true, false, false, true, false }),

        // Whole Tone: W-W-W-W-W-W
        new ScaleDefinition("Whole Tone", new[] { true, false, true, false, true, false, true, false, true, false, true, false })
    };

    /// <summary>
    /// Gets the note bypass states.
    /// </summary>
    public ObservableCollection<NoteBypassState> NoteBypassStates { get; } = new();

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a parameter changes.
    /// </summary>
    public event EventHandler<AutoTuneParameterEventArgs>? ParameterChanged;

    /// <summary>
    /// Event raised when the pitch data is updated.
    /// </summary>
    public event EventHandler<PitchUpdateEventArgs>? PitchUpdated;

    #endregion

    #region Constructor

    public AutoTuneViewModel()
    {
        // Initialize note bypass states
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
        for (int i = 0; i < 12; i++)
        {
            var state = new NoteBypassState(i, noteNames[i]);
            state.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(NoteBypassState.IsBypassed) && s is NoteBypassState ns)
                {
                    _bypassedNotes[ns.NoteIndex] = ns.IsBypassed;
                    OnNoteBypassChanged();
                }
            };
            NoteBypassStates.Add(state);
        }
    }

    #endregion

    #region Property Changed Handlers

    partial void OnRootNoteChanged(int value)
    {
        RaiseParameterChanged("RootNote", value);
        UpdateScaleNotes();
    }

    partial void OnScaleTypeChanged(int value)
    {
        RaiseParameterChanged("ScaleType", value);
        UpdateScaleNotes();
    }

    partial void OnCorrectionSpeedChanged(double value)
    {
        RaiseParameterChanged("CorrectionSpeed", (float)(value / 100.0));
    }

    partial void OnHumanizeAmountChanged(double value)
    {
        RaiseParameterChanged("Humanize", (float)(value / 100.0));
    }

    partial void OnRetuneSpeedChanged(double value)
    {
        RaiseParameterChanged("RetuneSpeed", (float)value);
    }

    partial void OnFormantPreservationChanged(bool value)
    {
        RaiseParameterChanged("FormantPreservation", value ? 1.0f : 0.0f);
    }

    partial void OnIsBypassedChanged(bool value)
    {
        RaiseParameterChanged("Bypass", value ? 1.0f : 0.0f);
    }

    partial void OnInputPitchChanged(double value)
    {
        UpdatePitchDisplay(value, true);
    }

    partial void OnOutputPitchChanged(double value)
    {
        UpdatePitchDisplay(value, false);
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void SetPreset(string presetName)
    {
        switch (presetName)
        {
            case "Subtle":
                CorrectionSpeed = 30;
                HumanizeAmount = 40;
                RetuneSpeed = 100;
                FormantPreservation = true;
                PresetName = "Subtle Correction";
                break;

            case "Natural":
                CorrectionSpeed = 50;
                HumanizeAmount = 25;
                RetuneSpeed = 80;
                FormantPreservation = true;
                PresetName = "Natural Voice";
                break;

            case "Standard":
                CorrectionSpeed = 70;
                HumanizeAmount = 15;
                RetuneSpeed = 50;
                FormantPreservation = true;
                PresetName = "Standard";
                break;

            case "Hard":
                CorrectionSpeed = 90;
                HumanizeAmount = 5;
                RetuneSpeed = 20;
                FormantPreservation = true;
                PresetName = "Hard Correction";
                break;

            case "T-Pain":
                CorrectionSpeed = 100;
                HumanizeAmount = 0;
                RetuneSpeed = 0;
                FormantPreservation = false;
                PresetName = "T-Pain Effect";
                break;

            case "Cher":
                CorrectionSpeed = 100;
                HumanizeAmount = 0;
                RetuneSpeed = 0;
                FormantPreservation = true;
                PresetName = "Cher Effect";
                break;
        }

        StatusMessage = $"Preset applied: {PresetName}";
    }

    [RelayCommand]
    private void Reset()
    {
        RootNote = 0;
        ScaleType = 0;
        CorrectionSpeed = 100;
        HumanizeAmount = 0;
        RetuneSpeed = 50;
        FormantPreservation = true;
        IsBypassed = false;

        foreach (var state in NoteBypassStates)
        {
            state.IsBypassed = false;
        }

        PresetName = "Default";
        StatusMessage = "Reset to defaults";
    }

    [RelayCommand]
    private void ClearNoteBypass()
    {
        foreach (var state in NoteBypassStates)
        {
            state.IsBypassed = false;
        }
        StatusMessage = "Note bypass cleared";
    }

    [RelayCommand]
    private void ToggleNoteBypassed(int noteIndex)
    {
        if (noteIndex >= 0 && noteIndex < NoteBypassStates.Count)
        {
            NoteBypassStates[noteIndex].IsBypassed = !NoteBypassStates[noteIndex].IsBypassed;
        }
    }

    [RelayCommand]
    private void SetScaleNotesFromCurrentScale()
    {
        // Auto-set bypassed notes based on selected scale
        if (ScaleType >= 0 && ScaleType < ScaleTypes.Count)
        {
            var scale = ScaleTypes[ScaleType];
            for (int i = 0; i < 12; i++)
            {
                // Bypass notes that are NOT in the scale
                int transposedNote = (i - RootNote + 12) % 12;
                NoteBypassStates[i].IsBypassed = !scale.Notes[transposedNote];
            }
        }

        StatusMessage = "Note bypass set from scale";
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Updates the detected pitch values.
    /// </summary>
    public void UpdatePitchValues(double inputPitchHz, double outputPitchHz)
    {
        InputPitch = inputPitchHz;
        OutputPitch = outputPitchHz;

        PitchUpdated?.Invoke(this, new PitchUpdateEventArgs(inputPitchHz, outputPitchHz));
    }

    /// <summary>
    /// Gets the allowed notes based on current scale and bypass settings.
    /// </summary>
    public bool[] GetAllowedNotes()
    {
        var allowed = new bool[12];

        if (ScaleType >= 0 && ScaleType < ScaleTypes.Count)
        {
            var scale = ScaleTypes[ScaleType];
            for (int i = 0; i < 12; i++)
            {
                int transposedNote = (i - RootNote + 12) % 12;
                allowed[i] = scale.Notes[transposedNote] && !_bypassedNotes[i];
            }
        }

        return allowed;
    }

    /// <summary>
    /// Gets the bypassed notes array.
    /// </summary>
    public bool[] GetBypassedNotes()
    {
        return (bool[])_bypassedNotes.Clone();
    }

    #endregion

    #region Private Methods

    private void UpdatePitchDisplay(double pitchHz, bool isInput)
    {
        if (pitchHz <= 0)
        {
            if (isInput)
            {
                InputPitchDisplay = "--";
                InputPitchCents = 0;
            }
            else
            {
                OutputPitchDisplay = "--";
                OutputPitchCents = 0;
            }
            return;
        }

        var (note, octave, cents) = FrequencyToNoteInfo(pitchHz);

        if (isInput)
        {
            InputPitchDisplay = $"{note}{octave}";
            InputPitchCents = cents;
        }
        else
        {
            OutputPitchDisplay = $"{note}{octave}";
            OutputPitchCents = cents;
        }
    }

    private static (string note, int octave, int cents) FrequencyToNoteInfo(double frequency)
    {
        string[] noteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        // A4 = 440 Hz, MIDI note 69
        double midiNote = 69 + 12 * Math.Log2(frequency / 440.0);
        int nearestNote = (int)Math.Round(midiNote);
        int cents = (int)Math.Round((midiNote - nearestNote) * 100);

        int noteIndex = nearestNote % 12;
        if (noteIndex < 0) noteIndex += 12;
        int octave = (nearestNote / 12) - 1;

        return (noteNames[noteIndex], octave, cents);
    }

    private void UpdateScaleNotes()
    {
        // Notify that scale configuration has changed
        RaiseParameterChanged("Scale", 0);
    }

    private void OnNoteBypassChanged()
    {
        int bypassCount = 0;
        foreach (var bypassed in _bypassedNotes)
        {
            if (bypassed) bypassCount++;
        }

        StatusMessage = bypassCount > 0 ? $"{bypassCount} note(s) bypassed" : "All notes active";
        RaiseParameterChanged("NoteBypass", 0);
    }

    private void RaiseParameterChanged(string name, float value)
    {
        ParameterChanged?.Invoke(this, new AutoTuneParameterEventArgs(name, value));
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        GC.SuppressFinalize(this);
    }

    #endregion
}

/// <summary>
/// Represents the bypass state for a single note.
/// </summary>
public partial class NoteBypassState : ObservableObject
{
    /// <summary>
    /// Gets the note index (0-11).
    /// </summary>
    public int NoteIndex { get; }

    /// <summary>
    /// Gets the note name.
    /// </summary>
    public string NoteName { get; }

    [ObservableProperty]
    private bool _isBypassed;

    public NoteBypassState(int noteIndex, string noteName)
    {
        NoteIndex = noteIndex;
        NoteName = noteName;
    }
}

/// <summary>
/// Event arguments for auto-tune parameter changes.
/// </summary>
public class AutoTuneParameterEventArgs : EventArgs
{
    /// <summary>
    /// Gets the parameter name.
    /// </summary>
    public string ParameterName { get; }

    /// <summary>
    /// Gets the parameter value.
    /// </summary>
    public float Value { get; }

    public AutoTuneParameterEventArgs(string parameterName, float value)
    {
        ParameterName = parameterName;
        Value = value;
    }
}

/// <summary>
/// Event arguments for pitch updates.
/// </summary>
public class PitchUpdateEventArgs : EventArgs
{
    /// <summary>
    /// Gets the input pitch in Hz.
    /// </summary>
    public double InputPitchHz { get; }

    /// <summary>
    /// Gets the output pitch in Hz.
    /// </summary>
    public double OutputPitchHz { get; }

    public PitchUpdateEventArgs(double inputPitchHz, double outputPitchHz)
    {
        InputPitchHz = inputPitchHz;
        OutputPitchHz = outputPitchHz;
    }
}

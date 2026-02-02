// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for the Beat Repeat effect control.

using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Effects;

/// <summary>
/// Represents a single step in the gate pattern for visualization.
/// </summary>
public partial class GatePatternStep : ObservableObject
{
    [ObservableProperty]
    private int _row;

    [ObservableProperty]
    private int _column;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private bool _isCurrentStep;
}

/// <summary>
/// ViewModel for the Beat Repeat effect control.
/// </summary>
public partial class BeatRepeatViewModel : ViewModelBase, IDisposable
{
    private bool _disposed;
    private DispatcherTimer? _repeatTimer;
    private readonly Random _random = new();

    #region Constants

    public const int PatternRows = 8;
    public const int PatternColumns = 16;

    #endregion

    #region Observable Properties

    [ObservableProperty]
    private BeatRepeatGridSize _gridSize = BeatRepeatGridSize.Eighth;

    [ObservableProperty]
    private int _repeatCount = 4;

    [ObservableProperty]
    private double _decay;

    [ObservableProperty]
    private int _pitchShift;

    [ObservableProperty]
    private double _probability = 100.0;

    [ObservableProperty]
    private double _mix = 100.0;

    [ObservableProperty]
    private bool _syncToTempo = true;

    [ObservableProperty]
    private bool _stutterMode;

    [ObservableProperty]
    private bool _isBypassed;

    [ObservableProperty]
    private bool _isRepeatActive;

    [ObservableProperty]
    private int _currentRepeatIndex;

    [ObservableProperty]
    private int _activeStepColumn;

    [ObservableProperty]
    private double _tempo = 120.0;

    #endregion

    #region Collections

    /// <summary>
    /// Gets the available grid sizes.
    /// </summary>
    public ObservableCollection<BeatRepeatGridSize> AvailableGridSizes { get; } = new(Enum.GetValues<BeatRepeatGridSize>());

    /// <summary>
    /// Gets the gate pattern steps for data binding.
    /// </summary>
    public ObservableCollection<GatePatternStep> GatePatternSteps { get; } = new();

    /// <summary>
    /// Gets the available presets.
    /// </summary>
    public ObservableCollection<string> AvailablePresets { get; } = new()
    {
        "Subtle",
        "Classic",
        "Glitch",
        "Tape Stop",
        "Riser",
        "Breakdown"
    };

    #endregion

    #region Computed Properties

    /// <summary>
    /// Gets the grid size display name.
    /// </summary>
    public string GridSizeDisplay => GridSize switch
    {
        BeatRepeatGridSize.Quarter => "1/4",
        BeatRepeatGridSize.Eighth => "1/8",
        BeatRepeatGridSize.Sixteenth => "1/16",
        BeatRepeatGridSize.ThirtySecond => "1/32",
        _ => "1/8"
    };

    /// <summary>
    /// Gets the pitch shift display string.
    /// </summary>
    public string PitchShiftDisplay
    {
        get
        {
            string sign = PitchShift > 0 ? "+" : "";
            return $"{sign}{PitchShift} st";
        }
    }

    /// <summary>
    /// Gets the repeat status display string.
    /// </summary>
    public string RepeatStatusDisplay => IsRepeatActive
        ? $"{CurrentRepeatIndex + 1} / {RepeatCount}"
        : "-- / --";

    /// <summary>
    /// Gets the interval in milliseconds for the current grid size and tempo.
    /// </summary>
    public double RepeatIntervalMs
    {
        get
        {
            double beatsPerSecond = Tempo / 60.0;
            double divisor = (int)GridSize;
            return (1000.0 / beatsPerSecond) / (divisor / 4.0);
        }
    }

    #endregion

    #region Events

    /// <summary>
    /// Event raised when a parameter changes.
    /// </summary>
    public event EventHandler<string>? ParameterChanged;

    /// <summary>
    /// Event raised when the gate pattern changes.
    /// </summary>
    public event EventHandler<bool[,]>? GatePatternChanged;

    /// <summary>
    /// Event raised when a repeat is triggered.
    /// </summary>
    public event EventHandler<int>? RepeatTriggered;

    #endregion

    #region Constructor

    public BeatRepeatViewModel()
    {
        InitializeGatePattern();
    }

    #endregion

    #region Initialization

    /// <summary>
    /// Initializes the gate pattern with all steps enabled.
    /// </summary>
    private void InitializeGatePattern()
    {
        GatePatternSteps.Clear();

        for (int row = 0; row < PatternRows; row++)
        {
            for (int col = 0; col < PatternColumns; col++)
            {
                GatePatternSteps.Add(new GatePatternStep
                {
                    Row = row,
                    Column = col,
                    IsActive = true,
                    IsCurrentStep = false
                });
            }
        }
    }

    #endregion

    #region Property Changed Handlers

    partial void OnGridSizeChanged(BeatRepeatGridSize value)
    {
        OnPropertyChanged(nameof(GridSizeDisplay));
        OnPropertyChanged(nameof(RepeatIntervalMs));
        ParameterChanged?.Invoke(this, nameof(GridSize));
        StatusMessage = $"Grid size: {GridSizeDisplay}";
    }

    partial void OnRepeatCountChanged(int value)
    {
        OnPropertyChanged(nameof(RepeatStatusDisplay));
        ParameterChanged?.Invoke(this, nameof(RepeatCount));
    }

    partial void OnDecayChanged(double value)
    {
        ParameterChanged?.Invoke(this, nameof(Decay));
    }

    partial void OnPitchShiftChanged(int value)
    {
        OnPropertyChanged(nameof(PitchShiftDisplay));
        ParameterChanged?.Invoke(this, nameof(PitchShift));
    }

    partial void OnProbabilityChanged(double value)
    {
        ParameterChanged?.Invoke(this, nameof(Probability));
    }

    partial void OnMixChanged(double value)
    {
        ParameterChanged?.Invoke(this, nameof(Mix));
    }

    partial void OnSyncToTempoChanged(bool value)
    {
        OnPropertyChanged(nameof(RepeatIntervalMs));
        ParameterChanged?.Invoke(this, nameof(SyncToTempo));
        StatusMessage = value ? "Sync to tempo enabled" : "Sync to tempo disabled";
    }

    partial void OnStutterModeChanged(bool value)
    {
        ParameterChanged?.Invoke(this, nameof(StutterMode));
        StatusMessage = value ? "Stutter mode enabled" : "Stutter mode disabled";
    }

    partial void OnIsBypassedChanged(bool value)
    {
        ParameterChanged?.Invoke(this, nameof(IsBypassed));
        StatusMessage = value ? "Effect bypassed" : "Effect active";
    }

    partial void OnTempoChanged(double value)
    {
        OnPropertyChanged(nameof(RepeatIntervalMs));
    }

    partial void OnIsRepeatActiveChanged(bool value)
    {
        OnPropertyChanged(nameof(RepeatStatusDisplay));
    }

    partial void OnCurrentRepeatIndexChanged(int value)
    {
        OnPropertyChanged(nameof(RepeatStatusDisplay));
    }

    #endregion

    #region Commands

    [RelayCommand]
    private void TogglePatternStep(GatePatternStep? step)
    {
        if (step == null) return;

        step.IsActive = !step.IsActive;
        RaiseGatePatternChanged();
    }

    [RelayCommand]
    private void ClearPattern()
    {
        foreach (var step in GatePatternSteps)
        {
            step.IsActive = false;
        }

        RaiseGatePatternChanged();
        StatusMessage = "Pattern cleared";
    }

    [RelayCommand]
    private void RandomizePattern()
    {
        foreach (var step in GatePatternSteps)
        {
            step.IsActive = _random.NextDouble() > 0.5;
        }

        RaiseGatePatternChanged();
        StatusMessage = "Random pattern generated";
    }

    [RelayCommand]
    private void GenerateEuclideanPattern()
    {
        for (int row = 0; row < PatternRows; row++)
        {
            int hits = row + 3; // 3-10 hits per row
            GenerateEuclideanRow(row, hits, PatternColumns);
        }

        RaiseGatePatternChanged();
        StatusMessage = "Euclidean pattern generated";
    }

    [RelayCommand]
    private void FillPattern()
    {
        foreach (var step in GatePatternSteps)
        {
            step.IsActive = true;
        }

        RaiseGatePatternChanged();
        StatusMessage = "Pattern filled";
    }

    [RelayCommand]
    private void InvertPattern()
    {
        foreach (var step in GatePatternSteps)
        {
            step.IsActive = !step.IsActive;
        }

        RaiseGatePatternChanged();
        StatusMessage = "Pattern inverted";
    }

    [RelayCommand]
    private void ApplyPreset(string? presetName)
    {
        if (string.IsNullOrEmpty(presetName)) return;

        switch (presetName)
        {
            case "Subtle":
                GridSize = BeatRepeatGridSize.Eighth;
                RepeatCount = 2;
                Decay = 30;
                PitchShift = 0;
                Probability = 50;
                Mix = 60;
                SyncToTempo = true;
                StutterMode = false;
                break;

            case "Classic":
                GridSize = BeatRepeatGridSize.Eighth;
                RepeatCount = 4;
                Decay = 15;
                PitchShift = 0;
                Probability = 100;
                Mix = 100;
                SyncToTempo = true;
                StutterMode = false;
                break;

            case "Glitch":
                GridSize = BeatRepeatGridSize.Sixteenth;
                RepeatCount = 8;
                Decay = 0;
                PitchShift = 0;
                Probability = 70;
                Mix = 100;
                SyncToTempo = false;
                StutterMode = true;
                break;

            case "Tape Stop":
                GridSize = BeatRepeatGridSize.Eighth;
                RepeatCount = 6;
                Decay = 50;
                PitchShift = -2;
                Probability = 100;
                Mix = 100;
                SyncToTempo = true;
                StutterMode = false;
                break;

            case "Riser":
                GridSize = BeatRepeatGridSize.Sixteenth;
                RepeatCount = 12;
                Decay = 0;
                PitchShift = 1;
                Probability = 100;
                Mix = 80;
                SyncToTempo = true;
                StutterMode = false;
                break;

            case "Breakdown":
                GridSize = BeatRepeatGridSize.ThirtySecond;
                RepeatCount = 16;
                Decay = 5;
                PitchShift = 0;
                Probability = 100;
                Mix = 100;
                SyncToTempo = true;
                StutterMode = true;
                break;
        }

        StatusMessage = $"Preset applied: {presetName}";
    }

    [RelayCommand]
    private void ResetParameters()
    {
        GridSize = BeatRepeatGridSize.Eighth;
        RepeatCount = 4;
        Decay = 0;
        PitchShift = 0;
        Probability = 100;
        Mix = 100;
        SyncToTempo = true;
        StutterMode = false;
        IsBypassed = false;

        // Reset pattern to all enabled
        foreach (var step in GatePatternSteps)
        {
            step.IsActive = true;
        }

        RaiseGatePatternChanged();
        StatusMessage = "Reset to defaults";
    }

    [RelayCommand]
    private void TriggerRepeat()
    {
        if (IsBypassed) return;

        // Check probability
        if (_random.NextDouble() * 100 > Probability) return;

        IsRepeatActive = true;
        CurrentRepeatIndex = 0;

        // Start repeat timer
        StartRepeatTimer();
    }

    [RelayCommand]
    private void StopRepeat()
    {
        IsRepeatActive = false;
        CurrentRepeatIndex = 0;
        _repeatTimer?.Stop();

        // Reset current step markers
        foreach (var step in GatePatternSteps)
        {
            step.IsCurrentStep = false;
        }
    }

    #endregion

    #region Pattern Methods

    /// <summary>
    /// Generates a Euclidean rhythm pattern for a row.
    /// </summary>
    private void GenerateEuclideanRow(int row, int hits, int steps)
    {
        hits = Math.Min(hits, steps);

        var pattern = new bool[steps];

        if (hits == 0)
        {
            // All off
        }
        else if (hits == steps)
        {
            for (int i = 0; i < steps; i++)
            {
                pattern[i] = true;
            }
        }
        else
        {
            // Bresenham-based Euclidean rhythm
            int prev = -1;
            for (int i = 0; i < hits; i++)
            {
                int current = (int)Math.Floor((double)(i * steps) / hits);
                if (current != prev)
                {
                    pattern[current] = true;
                    prev = current;
                }
            }
        }

        // Apply to steps
        for (int col = 0; col < steps; col++)
        {
            var step = GetStep(row, col);
            if (step != null)
            {
                step.IsActive = pattern[col];
            }
        }
    }

    /// <summary>
    /// Gets the step at the specified row and column.
    /// </summary>
    private GatePatternStep? GetStep(int row, int col)
    {
        int index = row * PatternColumns + col;
        return index < GatePatternSteps.Count ? GatePatternSteps[index] : null;
    }

    /// <summary>
    /// Gets the gate pattern as a 2D array.
    /// </summary>
    public bool[,] GetGatePattern()
    {
        var pattern = new bool[PatternRows, PatternColumns];

        foreach (var step in GatePatternSteps)
        {
            if (step.Row < PatternRows && step.Column < PatternColumns)
            {
                pattern[step.Row, step.Column] = step.IsActive;
            }
        }

        return pattern;
    }

    /// <summary>
    /// Sets the gate pattern from a 2D array.
    /// </summary>
    public void SetGatePattern(bool[,] pattern)
    {
        if (pattern.GetLength(0) != PatternRows || pattern.GetLength(1) != PatternColumns)
        {
            throw new ArgumentException($"Pattern must be {PatternRows}x{PatternColumns}");
        }

        foreach (var step in GatePatternSteps)
        {
            if (step.Row < PatternRows && step.Column < PatternColumns)
            {
                step.IsActive = pattern[step.Row, step.Column];
            }
        }
    }

    /// <summary>
    /// Checks if the pattern step at row/column is active.
    /// </summary>
    public bool IsStepActive(int row, int column)
    {
        var step = GetStep(row, column);
        return step?.IsActive ?? false;
    }

    #endregion

    #region Repeat Timer

    /// <summary>
    /// Starts the repeat timer for visualization.
    /// </summary>
    private void StartRepeatTimer()
    {
        _repeatTimer?.Stop();

        _repeatTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(RepeatIntervalMs)
        };

        _repeatTimer.Tick += RepeatTimer_Tick;
        _repeatTimer.Start();
    }

    private void RepeatTimer_Tick(object? sender, EventArgs e)
    {
        if (!IsRepeatActive)
        {
            _repeatTimer?.Stop();
            return;
        }

        // Update current step visualization
        UpdateCurrentStepVisualization();

        // Calculate volume decay
        double volumeMultiplier = 1.0 - (CurrentRepeatIndex * (Decay / 100.0) / RepeatCount);
        volumeMultiplier = Math.Max(0, volumeMultiplier);

        // Calculate pitch shift
        int totalPitchShift = PitchShift * CurrentRepeatIndex;

        // Trigger the repeat
        RepeatTriggered?.Invoke(this, CurrentRepeatIndex);

        // Move to next repeat
        CurrentRepeatIndex++;

        if (CurrentRepeatIndex >= RepeatCount)
        {
            StopRepeat();
        }
    }

    private void UpdateCurrentStepVisualization()
    {
        foreach (var step in GatePatternSteps)
        {
            step.IsCurrentStep = step.Column == ActiveStepColumn;
        }

        ActiveStepColumn = (ActiveStepColumn + 1) % PatternColumns;
    }

    #endregion

    #region Helper Methods

    private void RaiseGatePatternChanged()
    {
        GatePatternChanged?.Invoke(this, GetGatePattern());
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _repeatTimer?.Stop();
        _repeatTimer = null;
    }

    #endregion
}

/// <summary>
/// Beat grid size divisions for the ViewModel.
/// </summary>
public enum BeatRepeatGridSize
{
    /// <summary>1/4 beat division (quarter notes)</summary>
    Quarter = 4,
    /// <summary>1/8 beat division (eighth notes)</summary>
    Eighth = 8,
    /// <summary>1/16 beat division (sixteenth notes)</summary>
    Sixteenth = 16,
    /// <summary>1/32 beat division (thirty-second notes)</summary>
    ThirtySecond = 32
}

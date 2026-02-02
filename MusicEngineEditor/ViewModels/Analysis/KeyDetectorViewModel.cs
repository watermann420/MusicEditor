// MusicEngine License (MEL) - Honor-Based Commercial Support
// Copyright (c) 2025-2026 Yannis Watermann (watermann420, nullonebinary)
// https://github.com/watermann420/MusicEngineEditor
// Description: ViewModel for key detection and analysis.

using System;
using System.Collections.ObjectModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace MusicEngineEditor.ViewModels.Analysis;

/// <summary>
/// ViewModel for key detection panel providing musical key analysis,
/// circle of fifths visualization, chromagram display, and key change detection.
/// </summary>
public partial class KeyDetectorViewModel : ViewModelBase
{
    #region Constants

    private static readonly string[] NoteNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private static readonly string[] NoteNamesFlat = { "C", "Db", "D", "Eb", "E", "F", "Gb", "G", "Ab", "A", "Bb", "B" };

    private static readonly string[] ModeNames =
    {
        "Ionian (Major)", "Dorian", "Phrygian", "Lydian",
        "Mixolydian", "Aeolian (Minor)", "Locrian"
    };

    // Major key profiles (Krumhansl-Schmuckler)
    private static readonly double[] MajorProfile =
    {
        6.35, 2.23, 3.48, 2.33, 4.38, 4.09, 2.52, 5.19, 2.39, 3.66, 2.29, 2.88
    };

    // Minor key profiles (Krumhansl-Schmuckler)
    private static readonly double[] MinorProfile =
    {
        6.33, 2.68, 3.52, 5.38, 2.60, 3.53, 2.54, 4.75, 3.98, 2.69, 3.34, 3.17
    };

    #endregion

    #region Observable Properties - Key Detection

    [ObservableProperty]
    private string _detectedKey = "---";

    [ObservableProperty]
    private int _detectedKeyIndex = -1;

    [ObservableProperty]
    private bool _isMajor = true;

    [ObservableProperty]
    private double _confidence;

    [ObservableProperty]
    private string _confidenceText = "0%";

    [ObservableProperty]
    private string _confidenceLevel = "Unknown";

    [ObservableProperty]
    private string _relativeMajorMinor = "---";

    [ObservableProperty]
    private string _parallelMajorMinor = "---";

    [ObservableProperty]
    private int _detectedModeIndex;

    [ObservableProperty]
    private string _detectedMode = "Ionian (Major)";

    #endregion

    #region Observable Properties - Chromagram

    [ObservableProperty]
    private double[] _chromagram = new double[12];

    [ObservableProperty]
    private double[] _normalizedChromagram = new double[12];

    [ObservableProperty]
    private bool[] _scaleDegreePresent = new bool[12];

    [ObservableProperty]
    private double[] _scaleDegreeStrength = new double[12];

    #endregion

    #region Observable Properties - Alternative Keys

    [ObservableProperty]
    private ObservableCollection<AlternativeKeyViewModel> _alternativeKeys = new();

    #endregion

    #region Observable Properties - Key Changes

    [ObservableProperty]
    private ObservableCollection<KeyChangeViewModel> _keyChanges = new();

    [ObservableProperty]
    private bool _hasKeyChanges;

    [ObservableProperty]
    private double _audioDuration;

    #endregion

    #region Observable Properties - UI State

    [ObservableProperty]
    private bool _isAnalyzing;

    [ObservableProperty]
    private bool _isLiveMode = true;

    [ObservableProperty]
    private bool _useFlats;

    [ObservableProperty]
    private bool _canExportKey;

    #endregion

    #region Events

    /// <summary>
    /// Event raised when detected key should be exported to project settings.
    /// </summary>
    public event EventHandler<KeyExportEventArgs>? ExportKeyRequested;

    /// <summary>
    /// Event raised when analysis completes.
    /// </summary>
    public event EventHandler<KeyAnalysisResult>? AnalysisCompleted;

    #endregion

    #region Constructor

    public KeyDetectorViewModel()
    {
        InitializeChromagram();
    }

    private void InitializeChromagram()
    {
        for (int i = 0; i < 12; i++)
        {
            _chromagram[i] = 0;
            _normalizedChromagram[i] = 0;
            _scaleDegreePresent[i] = false;
            _scaleDegreeStrength[i] = 0;
        }
    }

    #endregion

    #region Commands

    [RelayCommand]
    private async Task AnalyzeAudioAsync(float[]? samples)
    {
        if (samples == null || samples.Length == 0)
            return;

        IsAnalyzing = true;
        IsBusy = true;
        StatusMessage = "Analyzing audio for key detection...";

        try
        {
            await Task.Run(() => PerformKeyAnalysis(samples, 44100));
            StatusMessage = $"Detected: {DetectedKey}";
            CanExportKey = DetectedKeyIndex >= 0;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsAnalyzing = false;
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void ToggleLiveMode()
    {
        IsLiveMode = !IsLiveMode;
    }

    [RelayCommand]
    private void ToggleFlats()
    {
        UseFlats = !UseFlats;
        UpdateKeyDisplay();
    }

    [RelayCommand]
    private void ExportToProject()
    {
        if (DetectedKeyIndex < 0) return;

        var args = new KeyExportEventArgs
        {
            RootNote = DetectedKeyIndex,
            IsMajor = IsMajor,
            KeyName = DetectedKey,
            Confidence = Confidence,
            Mode = DetectedMode
        };

        ExportKeyRequested?.Invoke(this, args);
        StatusMessage = $"Exported {DetectedKey} to project settings";
    }

    [RelayCommand]
    private void Reset()
    {
        DetectedKey = "---";
        DetectedKeyIndex = -1;
        IsMajor = true;
        Confidence = 0;
        ConfidenceText = "0%";
        ConfidenceLevel = "Unknown";
        RelativeMajorMinor = "---";
        ParallelMajorMinor = "---";
        DetectedMode = "Ionian (Major)";
        DetectedModeIndex = 0;

        InitializeChromagram();
        OnPropertyChanged(nameof(Chromagram));
        OnPropertyChanged(nameof(NormalizedChromagram));
        OnPropertyChanged(nameof(ScaleDegreePresent));
        OnPropertyChanged(nameof(ScaleDegreeStrength));

        AlternativeKeys.Clear();
        KeyChanges.Clear();
        HasKeyChanges = false;
        CanExportKey = false;
        StatusMessage = "Ready";
    }

    [RelayCommand]
    private void SelectAlternativeKey(AlternativeKeyViewModel? key)
    {
        if (key == null) return;

        DetectedKeyIndex = key.RootNoteIndex;
        IsMajor = key.IsMajor;
        Confidence = key.Confidence;
        UpdateKeyDisplay();
        UpdateRelatedKeys();
        UpdateScaleDegrees();
    }

    #endregion

    #region Analysis Methods

    private void PerformKeyAnalysis(float[] samples, int sampleRate)
    {
        // Compute chromagram from audio
        ComputeChromagram(samples, sampleRate);

        // Detect key using correlation with key profiles
        DetectKey();

        // Detect mode
        DetectMode();

        // Find alternative key suggestions
        FindAlternativeKeys();

        // Detect key changes over time
        DetectKeyChanges(samples, sampleRate);

        // Update UI
        UpdateKeyDisplay();
        UpdateRelatedKeys();
        UpdateScaleDegrees();
    }

    private void ComputeChromagram(float[] samples, int sampleRate)
    {
        // Simple pitch class histogram using zero-crossing and energy analysis
        // In production, this would use FFT-based chromagram computation

        var pitchClassEnergy = new double[12];
        int frameSize = 4096;
        int hopSize = 2048;

        for (int frameStart = 0; frameStart + frameSize < samples.Length; frameStart += hopSize)
        {
            // Compute frame energy and estimated pitch
            double energy = 0;
            int zeroCrossings = 0;

            for (int i = 0; i < frameSize; i++)
            {
                int idx = frameStart + i;
                energy += samples[idx] * samples[idx];

                if (i > 0 && idx > 0)
                {
                    if ((samples[idx - 1] >= 0 && samples[idx] < 0) ||
                        (samples[idx - 1] < 0 && samples[idx] >= 0))
                    {
                        zeroCrossings++;
                    }
                }
            }

            energy = Math.Sqrt(energy / frameSize);

            // Estimate frequency from zero crossings
            double estimatedFreq = (zeroCrossings * sampleRate) / (2.0 * frameSize);

            // Map to pitch class (0-11)
            if (estimatedFreq > 20 && estimatedFreq < 5000 && energy > 0.001)
            {
                // Convert frequency to MIDI note number
                double midiNote = 12 * Math.Log2(estimatedFreq / 440.0) + 69;
                int pitchClass = ((int)Math.Round(midiNote) % 12 + 12) % 12;

                pitchClassEnergy[pitchClass] += energy;
            }
        }

        // Normalize chromagram
        double maxEnergy = 0.0001; // Prevent division by zero
        foreach (var e in pitchClassEnergy)
        {
            if (e > maxEnergy) maxEnergy = e;
        }

        for (int i = 0; i < 12; i++)
        {
            Chromagram[i] = pitchClassEnergy[i];
            NormalizedChromagram[i] = pitchClassEnergy[i] / maxEnergy;
        }

        OnPropertyChanged(nameof(Chromagram));
        OnPropertyChanged(nameof(NormalizedChromagram));
    }

    private void DetectKey()
    {
        double bestCorrelation = double.MinValue;
        int bestKey = 0;
        bool bestIsMajor = true;

        var alternativeScores = new List<(int key, bool isMajor, double correlation)>();

        // Test all major keys
        for (int key = 0; key < 12; key++)
        {
            double correlation = ComputeKeyCorrelation(key, true);
            alternativeScores.Add((key, true, correlation));

            if (correlation > bestCorrelation)
            {
                bestCorrelation = correlation;
                bestKey = key;
                bestIsMajor = true;
            }
        }

        // Test all minor keys
        for (int key = 0; key < 12; key++)
        {
            double correlation = ComputeKeyCorrelation(key, false);
            alternativeScores.Add((key, false, correlation));

            if (correlation > bestCorrelation)
            {
                bestCorrelation = correlation;
                bestKey = key;
                bestIsMajor = false;
            }
        }

        // Set detected key
        DetectedKeyIndex = bestKey;
        IsMajor = bestIsMajor;

        // Calculate confidence based on correlation difference
        alternativeScores.Sort((a, b) => b.correlation.CompareTo(a.correlation));

        if (alternativeScores.Count >= 2)
        {
            double diff = alternativeScores[0].correlation - alternativeScores[1].correlation;
            Confidence = Math.Min(1.0, Math.Max(0, diff * 5 + 0.5));
        }
        else
        {
            Confidence = bestCorrelation > 0 ? 0.5 : 0;
        }

        UpdateConfidenceDisplay();
    }

    private double ComputeKeyCorrelation(int rootNote, bool isMajor)
    {
        var profile = isMajor ? MajorProfile : MinorProfile;
        double correlation = 0;
        double sumProfile = 0;
        double sumChroma = 0;

        for (int i = 0; i < 12; i++)
        {
            int rotatedIndex = (i + rootNote) % 12;
            correlation += NormalizedChromagram[rotatedIndex] * profile[i];
            sumProfile += profile[i] * profile[i];
            sumChroma += NormalizedChromagram[rotatedIndex] * NormalizedChromagram[rotatedIndex];
        }

        // Pearson correlation
        double denominator = Math.Sqrt(sumProfile * sumChroma);
        return denominator > 0 ? correlation / denominator : 0;
    }

    private void DetectMode()
    {
        if (DetectedKeyIndex < 0) return;

        // Analyze which mode best fits the chromagram pattern
        // Modes are rotations of the major scale starting from different degrees

        int[] modeOffsets = { 0, 2, 4, 5, 7, 9, 11 }; // Semitone offsets for each mode
        double bestModeScore = double.MinValue;
        int bestMode = 0;

        for (int mode = 0; mode < 7; mode++)
        {
            double score = ComputeModeScore(mode);
            if (score > bestModeScore)
            {
                bestModeScore = score;
                bestMode = mode;
            }
        }

        DetectedModeIndex = bestMode;
        DetectedMode = ModeNames[bestMode];
    }

    private double ComputeModeScore(int modeIndex)
    {
        // Mode intervals from root (in semitones)
        int[][] modeIntervals =
        {
            new[] { 0, 2, 4, 5, 7, 9, 11 },  // Ionian (Major)
            new[] { 0, 2, 3, 5, 7, 9, 10 },  // Dorian
            new[] { 0, 1, 3, 5, 7, 8, 10 },  // Phrygian
            new[] { 0, 2, 4, 6, 7, 9, 11 },  // Lydian
            new[] { 0, 2, 4, 5, 7, 9, 10 },  // Mixolydian
            new[] { 0, 2, 3, 5, 7, 8, 10 },  // Aeolian (Minor)
            new[] { 0, 1, 3, 5, 6, 8, 10 }   // Locrian
        };

        double score = 0;
        var intervals = modeIntervals[modeIndex];

        foreach (int interval in intervals)
        {
            int pitchClass = (DetectedKeyIndex + interval) % 12;
            score += NormalizedChromagram[pitchClass];
        }

        return score;
    }

    private void FindAlternativeKeys()
    {
        AlternativeKeys.Clear();

        var scores = new List<(int key, bool isMajor, double correlation)>();

        // Compute all key correlations
        for (int key = 0; key < 12; key++)
        {
            scores.Add((key, true, ComputeKeyCorrelation(key, true)));
            scores.Add((key, false, ComputeKeyCorrelation(key, false)));
        }

        // Sort by correlation
        scores.Sort((a, b) => b.correlation.CompareTo(a.correlation));

        // Take top 5 alternatives (excluding the detected key)
        int count = 0;
        foreach (var (key, isMajor, correlation) in scores)
        {
            if (key == DetectedKeyIndex && isMajor == IsMajor)
                continue;

            if (count >= 5)
                break;

            // Normalize to confidence percentage
            double confidence = Math.Max(0, Math.Min(1, (correlation + 1) / 2));

            AlternativeKeys.Add(new AlternativeKeyViewModel
            {
                RootNoteIndex = key,
                IsMajor = isMajor,
                KeyName = GetKeyName(key, isMajor),
                Confidence = confidence,
                ConfidenceText = $"{confidence * 100:F0}%"
            });

            count++;
        }
    }

    private void DetectKeyChanges(float[] samples, int sampleRate)
    {
        KeyChanges.Clear();

        // Analyze key in segments to detect modulations
        double segmentDuration = 5.0; // 5 second segments
        int segmentSamples = (int)(segmentDuration * sampleRate);

        AudioDuration = (double)samples.Length / sampleRate;
        int numSegments = (int)Math.Ceiling((double)samples.Length / segmentSamples);

        if (numSegments <= 1)
        {
            HasKeyChanges = false;
            return;
        }

        int? previousKey = null;
        bool? previousIsMajor = null;

        for (int seg = 0; seg < numSegments; seg++)
        {
            int start = seg * segmentSamples;
            int length = Math.Min(segmentSamples, samples.Length - start);

            if (length < sampleRate) // Skip segments shorter than 1 second
                continue;

            var segmentSamples2 = new float[length];
            Array.Copy(samples, start, segmentSamples2, 0, length);

            // Analyze segment
            var segmentChroma = new double[12];
            ComputeSegmentChromagram(segmentSamples2, sampleRate, segmentChroma);

            // Detect key for segment
            var (segKey, segIsMajor, segConfidence) = DetectKeyFromChromagram(segmentChroma);

            // Check for key change
            if (previousKey.HasValue && (segKey != previousKey || segIsMajor != previousIsMajor))
            {
                if (segConfidence > 0.4) // Only report confident key changes
                {
                    double timePosition = (double)start / sampleRate;
                    KeyChanges.Add(new KeyChangeViewModel
                    {
                        TimePosition = timePosition,
                        TimeText = FormatTime(timePosition),
                        FromKey = GetKeyName(previousKey.Value, previousIsMajor ?? true),
                        ToKey = GetKeyName(segKey, segIsMajor),
                        Confidence = segConfidence
                    });
                }
            }

            previousKey = segKey;
            previousIsMajor = segIsMajor;
        }

        HasKeyChanges = KeyChanges.Count > 0;
    }

    private void ComputeSegmentChromagram(float[] samples, int sampleRate, double[] chromagram)
    {
        int frameSize = 2048;
        int hopSize = 1024;

        for (int i = 0; i < 12; i++)
            chromagram[i] = 0;

        for (int frameStart = 0; frameStart + frameSize < samples.Length; frameStart += hopSize)
        {
            double energy = 0;
            int zeroCrossings = 0;

            for (int i = 0; i < frameSize; i++)
            {
                int idx = frameStart + i;
                energy += samples[idx] * samples[idx];

                if (i > 0 && idx > 0)
                {
                    if ((samples[idx - 1] >= 0 && samples[idx] < 0) ||
                        (samples[idx - 1] < 0 && samples[idx] >= 0))
                    {
                        zeroCrossings++;
                    }
                }
            }

            energy = Math.Sqrt(energy / frameSize);
            double estimatedFreq = (zeroCrossings * sampleRate) / (2.0 * frameSize);

            if (estimatedFreq > 20 && estimatedFreq < 5000 && energy > 0.001)
            {
                double midiNote = 12 * Math.Log2(estimatedFreq / 440.0) + 69;
                int pitchClass = ((int)Math.Round(midiNote) % 12 + 12) % 12;
                chromagram[pitchClass] += energy;
            }
        }

        // Normalize
        double max = 0.0001;
        foreach (var e in chromagram)
            if (e > max) max = e;

        for (int i = 0; i < 12; i++)
            chromagram[i] /= max;
    }

    private (int key, bool isMajor, double confidence) DetectKeyFromChromagram(double[] chromagram)
    {
        double bestCorrelation = double.MinValue;
        int bestKey = 0;
        bool bestIsMajor = true;

        for (int key = 0; key < 12; key++)
        {
            double majorCorr = ComputeKeyCorrelationFromChroma(chromagram, key, true);
            double minorCorr = ComputeKeyCorrelationFromChroma(chromagram, key, false);

            if (majorCorr > bestCorrelation)
            {
                bestCorrelation = majorCorr;
                bestKey = key;
                bestIsMajor = true;
            }

            if (minorCorr > bestCorrelation)
            {
                bestCorrelation = minorCorr;
                bestKey = key;
                bestIsMajor = false;
            }
        }

        double confidence = Math.Max(0, Math.Min(1, (bestCorrelation + 1) / 2));
        return (bestKey, bestIsMajor, confidence);
    }

    private double ComputeKeyCorrelationFromChroma(double[] chromagram, int rootNote, bool isMajor)
    {
        var profile = isMajor ? MajorProfile : MinorProfile;
        double correlation = 0;
        double sumProfile = 0;
        double sumChroma = 0;

        for (int i = 0; i < 12; i++)
        {
            int rotatedIndex = (i + rootNote) % 12;
            correlation += chromagram[rotatedIndex] * profile[i];
            sumProfile += profile[i] * profile[i];
            sumChroma += chromagram[rotatedIndex] * chromagram[rotatedIndex];
        }

        double denominator = Math.Sqrt(sumProfile * sumChroma);
        return denominator > 0 ? correlation / denominator : 0;
    }

    #endregion

    #region Update Methods

    private void UpdateKeyDisplay()
    {
        if (DetectedKeyIndex < 0)
        {
            DetectedKey = "---";
            return;
        }

        DetectedKey = GetKeyName(DetectedKeyIndex, IsMajor);
    }

    private string GetKeyName(int keyIndex, bool isMajor)
    {
        string[] notes = UseFlats ? NoteNamesFlat : NoteNames;
        string quality = isMajor ? "Major" : "Minor";
        return $"{notes[keyIndex]} {quality}";
    }

    private void UpdateConfidenceDisplay()
    {
        ConfidenceText = $"{Confidence * 100:F0}%";

        ConfidenceLevel = Confidence switch
        {
            >= 0.7 => "High",
            >= 0.4 => "Medium",
            > 0 => "Low",
            _ => "Unknown"
        };
    }

    private void UpdateRelatedKeys()
    {
        if (DetectedKeyIndex < 0)
        {
            RelativeMajorMinor = "---";
            ParallelMajorMinor = "---";
            return;
        }

        // Relative major/minor (shares same key signature)
        // Relative minor is 3 semitones below major, relative major is 3 semitones above minor
        int relativeIndex = IsMajor
            ? (DetectedKeyIndex + 9) % 12  // Relative minor (down 3 = up 9)
            : (DetectedKeyIndex + 3) % 12; // Relative major (up 3)

        string[] notes = UseFlats ? NoteNamesFlat : NoteNames;
        RelativeMajorMinor = IsMajor
            ? $"{notes[relativeIndex]} Minor"
            : $"{notes[relativeIndex]} Major";

        // Parallel major/minor (same root note)
        ParallelMajorMinor = IsMajor
            ? $"{notes[DetectedKeyIndex]} Minor"
            : $"{notes[DetectedKeyIndex]} Major";
    }

    private void UpdateScaleDegrees()
    {
        if (DetectedKeyIndex < 0)
        {
            for (int i = 0; i < 12; i++)
            {
                ScaleDegreePresent[i] = false;
                ScaleDegreeStrength[i] = 0;
            }
            return;
        }

        // Scale intervals for major: W-W-H-W-W-W-H (2-2-1-2-2-2-1)
        // Scale intervals for minor: W-H-W-W-H-W-W (2-1-2-2-1-2-2)
        int[] majorIntervals = { 0, 2, 4, 5, 7, 9, 11 };
        int[] minorIntervals = { 0, 2, 3, 5, 7, 8, 10 };

        var intervals = IsMajor ? majorIntervals : minorIntervals;

        // Mark which pitch classes are in the scale
        var inScale = new bool[12];
        foreach (int interval in intervals)
        {
            int pitchClass = (DetectedKeyIndex + interval) % 12;
            inScale[pitchClass] = true;
        }

        // Update presence and strength for each pitch class
        double maxChroma = 0.0001;
        foreach (var c in NormalizedChromagram)
            if (c > maxChroma) maxChroma = c;

        for (int i = 0; i < 12; i++)
        {
            ScaleDegreePresent[i] = inScale[i] && NormalizedChromagram[i] > 0.1;
            ScaleDegreeStrength[i] = NormalizedChromagram[i];
        }

        OnPropertyChanged(nameof(ScaleDegreePresent));
        OnPropertyChanged(nameof(ScaleDegreeStrength));
    }

    #endregion

    #region Helper Methods

    private static string FormatTime(double seconds)
    {
        var ts = TimeSpan.FromSeconds(seconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}:{ts.Minutes:D2}:{ts.Seconds:D2}"
            : $"{ts.Minutes}:{ts.Seconds:D2}";
    }

    /// <summary>
    /// Updates with live chromagram data from audio engine.
    /// </summary>
    public void UpdateChromagram(double[] chromagramData)
    {
        if (!IsLiveMode || chromagramData.Length != 12)
            return;

        Array.Copy(chromagramData, Chromagram, 12);

        // Normalize
        double max = 0.0001;
        foreach (var c in Chromagram)
            if (c > max) max = c;

        for (int i = 0; i < 12; i++)
            NormalizedChromagram[i] = Chromagram[i] / max;

        OnPropertyChanged(nameof(Chromagram));
        OnPropertyChanged(nameof(NormalizedChromagram));

        // Update key detection
        DetectKey();
        DetectMode();
        UpdateKeyDisplay();
        UpdateRelatedKeys();
        UpdateScaleDegrees();
    }

    /// <summary>
    /// Gets the circle of fifths position for a key.
    /// </summary>
    public static int GetCircleOfFifthsPosition(int keyIndex, bool isMajor)
    {
        // Circle of fifths order: C, G, D, A, E, B, F#/Gb, Db, Ab, Eb, Bb, F
        int[] majorPositions = { 0, 7, 2, 9, 4, 11, 6, 1, 8, 3, 10, 5 };
        int[] minorPositions = { 9, 4, 11, 6, 1, 8, 3, 10, 5, 0, 7, 2 }; // Relative minors

        return isMajor ? majorPositions[keyIndex] : minorPositions[keyIndex];
    }

    /// <summary>
    /// Gets the angle in degrees for circle of fifths visualization.
    /// </summary>
    public static double GetCircleOfFifthsAngle(int keyIndex, bool isMajor)
    {
        int position = GetCircleOfFifthsPosition(keyIndex, isMajor);
        return position * 30.0; // 360 / 12 = 30 degrees per position
    }

    #endregion

    #region Public Methods

    /// <summary>
    /// Analyzes audio samples for key detection.
    /// </summary>
    public void AnalyzeAudio(float[] samples, int sampleRate = 44100)
    {
        _ = AnalyzeAudioAsync(samples);
    }

    /// <summary>
    /// Sets the key manually.
    /// </summary>
    public void SetManualKey(int keyIndex, bool isMajor)
    {
        DetectedKeyIndex = keyIndex;
        IsMajor = isMajor;
        Confidence = 1.0;
        UpdateKeyDisplay();
        UpdateConfidenceDisplay();
        UpdateRelatedKeys();
        UpdateScaleDegrees();
        CanExportKey = true;
    }

    #endregion
}

#region Supporting Classes

/// <summary>
/// ViewModel for alternative key suggestions.
/// </summary>
public partial class AlternativeKeyViewModel : ObservableObject
{
    [ObservableProperty]
    private int _rootNoteIndex;

    [ObservableProperty]
    private bool _isMajor;

    [ObservableProperty]
    private string _keyName = "";

    [ObservableProperty]
    private double _confidence;

    [ObservableProperty]
    private string _confidenceText = "";
}

/// <summary>
/// ViewModel for detected key changes.
/// </summary>
public partial class KeyChangeViewModel : ObservableObject
{
    [ObservableProperty]
    private double _timePosition;

    [ObservableProperty]
    private string _timeText = "";

    [ObservableProperty]
    private string _fromKey = "";

    [ObservableProperty]
    private string _toKey = "";

    [ObservableProperty]
    private double _confidence;
}

/// <summary>
/// Event arguments for key export.
/// </summary>
public class KeyExportEventArgs : EventArgs
{
    public int RootNote { get; set; }
    public bool IsMajor { get; set; }
    public string KeyName { get; set; } = "";
    public double Confidence { get; set; }
    public string Mode { get; set; } = "";
}

/// <summary>
/// Result of key analysis.
/// </summary>
public class KeyAnalysisResult
{
    public int DetectedKey { get; set; }
    public bool IsMajor { get; set; }
    public double Confidence { get; set; }
    public double[] Chromagram { get; set; } = new double[12];
    public string Mode { get; set; } = "";
    public List<KeyChangeViewModel> KeyChanges { get; set; } = new();
}

#endregion
